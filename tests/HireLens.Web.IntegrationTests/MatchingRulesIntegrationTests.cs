using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace HireLens.Web.IntegrationTests;

public sealed class MatchingRulesIntegrationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Candidates_With_More_Required_Skills_Get_Higher_Scores()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin,Recruiter");

        var createJobRequest = new
        {
            title = "Backend Engineer",
            description = "Build APIs with ASP.NET Core and C#.",
            requiredSkills = new[] { "C#", "ASP.NET Core" },
            optionalSkills = new[] { "Redis" },
            seniorityLevel = 2
        };

        var createJobResponse = await client.PostAsJsonAsync("/api/jobs", createJobRequest);
        createJobResponse.EnsureSuccessStatusCode();
        var createdJob = await createJobResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = createdJob.GetProperty("id").GetGuid();

        var strongCandidateId = await UploadCandidateAsync(
            client,
            "Strong Candidate",
            "strong.candidate@example.com",
            jobId,
            "C# ASP.NET Core Redis SQL Server microservices");

        var weakCandidateId = await UploadCandidateAsync(
            client,
            "Weak Candidate",
            "weak.candidate@example.com",
            jobId,
            "Excel Word PowerPoint operations reporting");

        var matchRequest = new
        {
            jobPostingId = jobId,
            candidateIds = new[] { strongCandidateId, weakCandidateId }
        };

        var matchesResponse = await client.PostAsJsonAsync("/api/matches/run", matchRequest);
        matchesResponse.EnsureSuccessStatusCode();
        var matches = await matchesResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(matches);
        Assert.Equal(2, matches!.Length);

        var strong = matches.First(x => x.GetProperty("candidateId").GetGuid() == strongCandidateId);
        var weak = matches.First(x => x.GetProperty("candidateId").GetGuid() == weakCandidateId);

        var strongScore = strong.GetProperty("matchScore").GetDouble();
        var weakScore = weak.GetProperty("matchScore").GetDouble();
        Assert.True(strongScore > weakScore, $"Expected strong score {strongScore} > weak score {weakScore}.");

        var strongMatchedSkills = strong.GetProperty("matchedSkills")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var weakMissingSkills = weak.GetProperty("missingSkills")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        Assert.Contains(strongMatchedSkills, x => string.Equals(x, "C#", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(weakMissingSkills, x => string.Equals(x, "ASP.NET Core", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Keywords_In_Interests_Section_Do_Not_Count_As_Core_Match_Evidence()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin,Recruiter");

        var createJobRequest = new
        {
            title = "Backend Engineer",
            description = "Build APIs with ASP.NET Core and C#.",
            requiredSkills = new[] { "C#", "ASP.NET Core" },
            optionalSkills = new[] { "Redis" },
            seniorityLevel = 2
        };

        var createJobResponse = await client.PostAsJsonAsync("/api/jobs", createJobRequest);
        createJobResponse.EnsureSuccessStatusCode();
        var createdJob = await createJobResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = createdJob.GetProperty("id").GetGuid();

        var focusedCandidateId = await UploadCandidateAsync(
            client,
            "Focused Candidate",
            "focused.candidate@example.com",
            jobId,
            """
            SKILLS
            C#, ASP.NET Core, SQL Server

            EXPERIENCE
            Built backend APIs and recruiter tools using ASP.NET Core and C#.
            """);

        var noisyCandidateId = await UploadCandidateAsync(
            client,
            "Noisy Candidate",
            "noisy.candidate@example.com",
            jobId,
            """
            SUMMARY
            Operations assistant focused on reports and admin coordination.

            EXPERIENCE
            Managed spreadsheets, schedules, and office operations.

            INTERESTS
            Learning C#, ASP.NET Core, Redis, microservices, and backend APIs.
            """);

        var matchRequest = new
        {
            jobPostingId = jobId,
            candidateIds = new[] { focusedCandidateId, noisyCandidateId }
        };

        var matchesResponse = await client.PostAsJsonAsync("/api/matches/run", matchRequest);
        matchesResponse.EnsureSuccessStatusCode();
        var matches = await matchesResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.NotNull(matches);
        Assert.Equal(2, matches!.Length);

        var focused = matches.First(x => x.GetProperty("candidateId").GetGuid() == focusedCandidateId);
        var noisy = matches.First(x => x.GetProperty("candidateId").GetGuid() == noisyCandidateId);

        Assert.True(
            focused.GetProperty("matchScore").GetDouble() > noisy.GetProperty("matchScore").GetDouble(),
            "Expected focused experience/skills to outrank noisy keyword mentions.");

        var noisyMissingSkills = noisy.GetProperty("missingSkills")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        Assert.Contains(noisyMissingSkills, x => string.Equals(x, "C#", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(noisyMissingSkills, x => string.Equals(x, "ASP.NET Core", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Guid> UploadCandidateAsync(
        HttpClient client,
        string fullName,
        string email,
        Guid jobId,
        string resumeText)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(fullName), "FullName" },
            { new StringContent(email), "Email" },
            { new StringContent(jobId.ToString()), "JobPostingId" }
        };

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(resumeText));
        content.Add(fileContent, "Resume", $"{fullName.Replace(' ', '_').ToLowerInvariant()}_resume.txt");

        var response = await client.PostAsync("/api/candidates/upload", content);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("id").GetGuid();
    }
}
