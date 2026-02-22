using HireLens.Application.Interfaces;
using HireLens.Infrastructure.Persistence;
using HireLens.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHireLensInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<HireLensDbContext>(options =>
        {
            if (string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.AddScoped<IResumeTextExtractor, ResumeTextExtractor>();
        services.AddScoped<IJobPostingService, JobPostingService>();
        services.AddScoped<ICandidateService, CandidateService>();

        return services;
    }
}
