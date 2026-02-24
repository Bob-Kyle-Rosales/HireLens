using HireLens.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Seeding;

public static class RoleSeeder
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RoleSeeder");
        var hostEnvironment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        var roles = new[] { "Admin", "Recruiter" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join("; ", roleResult.Errors.Select(x => x.Description));
                    throw new InvalidOperationException($"Failed to create role '{role}': {errors}");
                }
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogInformation("Seed admin credentials not configured. Skipping admin user creation.");
            return;
        }

        ValidateSeedAdminCredentials(adminEmail, adminPassword, hostEnvironment.IsDevelopment());

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Failed to create admin user '{adminEmail}': {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        if (!await userManager.IsInRoleAsync(adminUser, "Recruiter"))
        {
            await userManager.AddToRoleAsync(adminUser, "Recruiter");
        }
    }

    private static void ValidateSeedAdminCredentials(string email, string password, bool isDevelopment)
    {
        var insecureDefaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "changeme123!",
            "admin123!ab",
            "password",
            "p@ssw0rd!",
            "admin"
        };

        if (insecureDefaults.Contains(password))
        {
            throw new InvalidOperationException(
                "SeedAdmin password uses a known weak default. Provide a unique strong password.");
        }

        if (!isDevelopment && password.Length < 12)
        {
            throw new InvalidOperationException("SeedAdmin password must be at least 12 characters outside Development.");
        }

        if (!email.Contains('@'))
        {
            throw new InvalidOperationException("SeedAdmin email must be a valid email address.");
        }
    }
}
