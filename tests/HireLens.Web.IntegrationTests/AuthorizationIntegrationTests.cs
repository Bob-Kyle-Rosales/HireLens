using System.Net;
using Xunit;

namespace HireLens.Web.IntegrationTests;

public sealed class AuthorizationIntegrationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Recruiter_Cannot_Access_Admin_Reanalyze_Endpoint()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Recruiter");

        var response = await client.PostAsync("/api/models/reanalyze-candidates", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_User_Cannot_Access_Protected_Jobs_Endpoint()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Auth", "anonymous");

        var response = await client.GetAsync("/api/jobs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Can_Access_Admin_Reanalyze_Endpoint()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var response = await client.PostAsync("/api/models/reanalyze-candidates", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
