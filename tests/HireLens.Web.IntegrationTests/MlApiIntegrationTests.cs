using System.Net.Http.Json;
using System.Text;
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

    [Fact]
    public async Task Candidate_Upload_Requires_Job_And_Creates_Application_Context()
    {
        using var client = factory.CreateClient();

        var jobs = await client.GetFromJsonAsync<JsonElement[]>("/api/jobs");
        Assert.NotNull(jobs);
        Assert.NotEmpty(jobs!);

        var jobId = jobs![0].GetProperty("id").GetGuid().ToString();

        using var invalidContent = new MultipartFormDataContent
        {
            { new StringContent("Upload Tester"), "FullName" },
            { new StringContent("upload.tester@example.com"), "Email" }
        };

        var invalidFile = new ByteArrayContent(Encoding.UTF8.GetBytes("C# .NET"));
        invalidContent.Add(invalidFile, "Resume", "resume.txt");

        var invalidResponse = await client.PostAsync("/api/candidates/upload", invalidContent);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        using var validContent = new MultipartFormDataContent
        {
            { new StringContent("Upload Tester"), "FullName" },
            { new StringContent("upload.tester@example.com"), "Email" },
            { new StringContent(jobId), "JobPostingId" }
        };

        var validFile = new ByteArrayContent(Encoding.UTF8.GetBytes("C# .NET ASP.NET Core SQL Server Docker"));
        validContent.Add(validFile, "Resume", "resume.txt");

        var validResponse = await client.PostAsync("/api/candidates/upload", validContent);
        validResponse.EnsureSuccessStatusCode();

        var payload = await validResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(jobId, payload.GetProperty("latestAppliedJobPostingId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("latestAppliedJobTitle").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("latestApplicationStatus").GetString()));
    }
}
