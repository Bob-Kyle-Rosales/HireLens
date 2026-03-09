using HireLens.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HireLens.Web.Controllers;

[Route("auth")]
public sealed class AuthUiController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly UserManager<ApplicationUser> _userManager = userManager;

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

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterForm form)
    {
        var email = form.Email.Trim();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(form.Password) ||
            string.IsNullOrWhiteSpace(form.ConfirmPassword))
        {
            return Redirect(BuildRegisterRedirect("Email, password, and confirmation are required.", email, form.ReturnUrl));
        }

        if (!string.Equals(form.Password, form.ConfirmPassword, StringComparison.Ordinal))
        {
            return Redirect(BuildRegisterRedirect("Password and confirmation do not match.", email, form.ReturnUrl));
        }

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return Redirect(BuildRegisterRedirect("Email is already registered.", email, form.ReturnUrl));
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, form.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(x => x.Description));
            return Redirect(BuildRegisterRedirect(errors, email, form.ReturnUrl));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Recruiter");
        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(x => x.Description));
            return Redirect(BuildRegisterRedirect(errors, email, form.ReturnUrl));
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(GetSafeLocalReturnUrl(form.ReturnUrl));
    }

    private string BuildLoginRedirect(string error, string? returnUrl)
    {
        var safeReturnUrl = GetSafeLocalReturnUrl(returnUrl);
        return $"/login?error={Uri.EscapeDataString(error)}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
    }

    private string BuildRegisterRedirect(string error, string? email, string? returnUrl)
    {
        var safeReturnUrl = GetSafeLocalReturnUrl(returnUrl);
        return $"/register?error={Uri.EscapeDataString(error)}&email={Uri.EscapeDataString(email ?? string.Empty)}&returnUrl={Uri.EscapeDataString(safeReturnUrl)}";
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

    public sealed class RegisterForm
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string? ReturnUrl { get; set; }
    }
}
