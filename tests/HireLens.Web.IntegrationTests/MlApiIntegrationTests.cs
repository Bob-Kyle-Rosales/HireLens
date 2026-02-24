using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace HireLens.Web.IntegrationTests;

public sealed class MlApiIntegrationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Swagger_Exposes_Ml_Endpoints()
    {
        using var client = factory.CreateClient();
        var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = swagger.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/models/train/resume-category", out _));
        Assert.True(paths.TryGetProperty("/api/analyses/candidate/{candidateId}/run", out _));
        Assert.True(paths.TryGetProperty("/api/matches/run", out _));
    }

    [Fact]
    public async Task Admin_Training_Returns_Active_Model()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/models/train/resume-category", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ResumeCategoryClassifier", payload.GetProperty("modelType").GetString());
        Assert.True(payload.GetProperty("isActive").GetBoolean());
        Assert.InRange(payload.GetProperty("accuracy").GetDouble(), 0, 1);
    }

    [Fact]
    public async Task Analyze_And_Match_Endpoints_Return_Valid_Output()
    {
        using var client = factory.CreateClient();

        var jobs = await client.GetFromJsonAsync<JsonElement[]>("/api/jobs");
        var candidates = await client.GetFromJsonAsync<JsonElement[]>("/api/candidates");

        Assert.NotNull(jobs);
        Assert.NotNull(candidates);
        Assert.NotEmpty(jobs!);
        Assert.NotEmpty(candidates!);

        var jobId = jobs![0].GetProperty("id").GetGuid();
        var candidateId = candidates![0].GetProperty("id").GetGuid();

        var analysisResponse = await client.PostAsync($"/api/analyses/candidate/{candidateId}/run", content: null);
        analysisResponse.EnsureSuccessStatusCode();
        var analysis = await analysisResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(analysis.GetProperty("predictedCategory").GetString()));
        Assert.InRange(analysis.GetProperty("confidenceScore").GetDouble(), 0, 1);

        var matchRequest = new
        {
            jobPostingId = jobId,
            candidateIds = Array.Empty<Guid>()
        };

        var matchesResponse = await client.PostAsJsonAsync("/api/matches/run", matchRequest);
        matchesResponse.EnsureSuccessStatusCode();
        var matches = await matchesResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(matches);
        Assert.NotEmpty(matches!);

        var topMatch = matches![0];
        Assert.InRange(topMatch.GetProperty("matchScore").GetDouble(), 0, 100);
        Assert.Equal(JsonValueKind.Array, topMatch.GetProperty("matchedSkills").ValueKind);
        Assert.Equal(JsonValueKind.Array, topMatch.GetProperty("missingSkills").ValueKind);
        Assert.Equal(JsonValueKind.Array, topMatch.GetProperty("topOverlappingKeywords").ValueKind);
    }
}
