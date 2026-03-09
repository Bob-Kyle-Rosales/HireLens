using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace HireLens.Web.IntegrationTests;

public sealed class ModelLifecycleIntegrationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Train_Activate_And_Reanalyze_Flow_Works()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");

        var train1 = await client.PostAsync("/api/models/train/resume-category", content: null);
        train1.EnsureSuccessStatusCode();
        var model1 = await train1.Content.ReadFromJsonAsync<JsonElement>();
        var model1Id = model1.GetProperty("id").GetGuid();

        var activate = await client.PostAsync($"/api/models/{model1Id}/activate", content: null);
        Assert.Equal(HttpStatusCode.NoContent, activate.StatusCode);

        var active = await client.GetFromJsonAsync<JsonElement>("/api/models/active?modelType=ResumeCategoryClassifier");
        Assert.Equal(model1Id, active.GetProperty("id").GetGuid());
        Assert.True(active.GetProperty("isActive").GetBoolean());

        var reanalyze = await client.PostAsync("/api/models/reanalyze-candidates", content: null);
        reanalyze.EnsureSuccessStatusCode();
        var payload = await reanalyze.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(payload.GetProperty("analyzedCount").GetInt32() > 0);
    }
}
