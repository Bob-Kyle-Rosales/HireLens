using HireLens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HireLens.Web.IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"hirelens-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var modelDir = Path.Combine(Path.GetTempPath(), "hirelens-models-tests");
            var inMemorySettings = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "InMemory",
                ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:",
                ["SeedData:Enabled"] = "true",
                ["ML:ModelDirectory"] = modelDir,
                ["ML:Training:MinLabeledResumes"] = "6",
                ["ML:Training:MinDistinctCategories"] = "2"
            };

            configurationBuilder.AddInMemoryCollection(inMemorySettings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<HireLensDbContext>>();
            services.RemoveAll<HireLensDbContext>();

            services.AddDbContext<HireLensDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        });
    }
}
