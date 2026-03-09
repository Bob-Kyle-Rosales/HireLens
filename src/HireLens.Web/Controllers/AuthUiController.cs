using HireLens.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HireLens.Web.Controllers;

[Route("auth")]
public sealed class AuthUiController(SignInManager<ApplicationUser> signInManager) : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Email) || string.IsNullOrWhiteSpace(form.Password))
        {
            return Redirect(BuildLoginRedirect("Email and password are required.", form.ReturnUrl));
        }

        var result = await _signInManager.PasswordSignInAsync(
            form.Email.Trim(),
            form.Password,
            form.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return LocalRedirect(GetSafeLocalReturnUrl(form.ReturnUrl));
        }

        if (result.IsLockedOut)
        {
            return Redirect(BuildLoginRedirect("Account is locked. Try again later.", form.ReturnUrl));
        }

        if (result.RequiresTwoFactor)
        {
            return Redirect(BuildLoginRedirect("Two-factor authentication is required.", form.ReturnUrl));
        }

        return Redirect(BuildLoginRedirect("Invalid email or password.", form.ReturnUrl));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromForm] LogoutForm form)
    {
        await _signInManager.SignOutAsync();

        var returnUrl = GetSafeLocalReturnUrl(form.ReturnUrl);
        if (string.Equals(returnUrl, "/", StringComparison.Ordinal))
        {
            return Redirect("/login?loggedOut=1");
        }

        return LocalRedirect(returnUrl);
    }

    private string BuildLoginRedirect(string error, string? returnUrl)
    {
        var safeReturnUrl = GetSafeLocalReturnUrl(returnUrl);
        return $"/login?error={Uri.EscapeDataString(error)}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
    }

    private string GetSafeLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        return Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
    }

    public sealed class LoginForm
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public sealed class LogoutForm
    {
        public string? ReturnUrl { get; set; }
    }
}
