using HireLens.Domain.Entities;
using HireLens.Domain.Enums;
using HireLens.Infrastructure.Helpers;
using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Seeding;

public static class InitialDataSeeder
{
    private const string SeedUserId = "seed-system";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        if (!IsSeedEnabled(configuration))
        {
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HireLensDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("InitialDataSeeder");

        var hasAnyCoreData = await dbContext.JobPostings.AnyAsync() || await dbContext.Candidates.AnyAsync();
        if (hasAnyCoreData)
        {
            logger.LogInformation("Seed data skipped because existing job/candidate data was found.");
            return;
        }

        var now = DateTime.UtcNow;

        var modelVersion = new ModelVersion
        {
            Version = "resume-category-v1",
            ModelType = "ResumeCategoryClassifier",
            StoragePath = "models/resume-category-v1.zip",
            Accuracy = 0.84,
            IsActive = true,
            TrainedUtc = now.AddDays(-3)
        };

        var jobs = BuildJobs(now);
        var candidates = BuildCandidates(now);
        var analyses = BuildAnalyses(candidates, modelVersion, now);
        var matches = BuildMatches(jobs, candidates, analyses, now);

        dbContext.ModelVersions.Add(modelVersion);
        dbContext.JobPostings.AddRange(jobs);
        dbContext.Candidates.AddRange(candidates);
        dbContext.ResumeAnalyses.AddRange(analyses);
        dbContext.MatchResults.AddRange(matches);

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Seeded initial data: {Jobs} jobs, {Candidates} candidates, {Analyses} analyses, {Matches} matches.",
            jobs.Count,
            candidates.Count,
            analyses.Count,
            matches.Count);
    }

    private static bool IsSeedEnabled(IConfiguration configuration)
    {
        var raw = configuration["SeedData:Enabled"];
        return bool.TryParse(raw, out var enabled) && enabled;
    }

    private static List<JobPosting> BuildJobs(DateTime now)
    {
        return
        [
            new JobPosting
            {
                CreatedByUserId = SeedUserId,
                Title = "Senior .NET Backend Engineer",
                Description = "Build secure REST APIs with ASP.NET Core and EF Core, optimize SQL queries, and deploy to Azure using Docker.",
                RequiredSkills = TextProcessing.JoinSkillList(["C#", ".NET 8", "ASP.NET Core", "EF Core", "SQL Server", "Docker"]),
                OptionalSkills = TextProcessing.JoinSkillList(["Azure", "Redis", "Clean Architecture"]),
                SeniorityLevel = SeniorityLevel.Senior,
                CreatedUtc = now.AddDays(-9),
                UpdatedUtc = now.AddDays(-2)
            },
            new JobPosting
            {
                CreatedByUserId = SeedUserId,
                Title = "ML Engineer (NLP / Ranking)",
                Description = "Design text pipelines, build TF-IDF features, train and evaluate ranking and classification models, and productionize inference.",
                RequiredSkills = TextProcessing.JoinSkillList(["Python", "Machine Learning", "NLP", "TF-IDF", "Model Evaluation"]),
                OptionalSkills = TextProcessing.JoinSkillList(["ML.NET", "MLOps", "PostgreSQL"]),
                SeniorityLevel = SeniorityLevel.Mid,
                CreatedUtc = now.AddDays(-12),
                UpdatedUtc = now.AddDays(-4)
            },
            new JobPosting
            {
                CreatedByUserId = SeedUserId,
                Title = "Blazor Frontend Developer",
                Description = "Build interactive dashboards in Blazor with MudBlazor components, data tables, filters, and responsive UX.",
                RequiredSkills = TextProcessing.JoinSkillList(["Blazor", "C#", "MudBlazor", "HTML", "CSS"]),
                OptionalSkills = TextProcessing.JoinSkillList(["JavaScript", "REST API", "UX"]),
                SeniorityLevel = SeniorityLevel.Mid,
                CreatedUtc = now.AddDays(-7),
                UpdatedUtc = now.AddDays(-1)
            },
            new JobPosting
            {
                CreatedByUserId = SeedUserId,
                Title = "Technical Recruiter",
                Description = "Source candidates, screen resumes, coordinate interviews, and maintain high-quality candidate pipelines across engineering roles.",
                RequiredSkills = TextProcessing.JoinSkillList(["Sourcing", "Interviewing", "ATS", "Communication"]),
                OptionalSkills = TextProcessing.JoinSkillList(["Boolean Search", "Employer Branding"]),
                SeniorityLevel = SeniorityLevel.Junior,
                CreatedUtc = now.AddDays(-5),
                UpdatedUtc = now.AddDays(-1)
            }
        ];
    }

    private static List<Candidate> BuildCandidates(DateTime now)
    {
        return
        [
            new Candidate
            {
                UploadedByUserId = SeedUserId,
                FullName = "Alice Johnson",
                Email = "alice.johnson@example.com",
                ResumeFileName = "alice_johnson_resume.txt",
                ResumeContentType = "text/plain",
                ResumeText = """
                             Senior backend engineer with 7 years of experience in C#, .NET, ASP.NET Core, Entity Framework Core, SQL Server, Azure, and Docker.
                             Built scalable APIs, optimized query performance, and led migration from monolith to microservices.
                             """,
                CreatedUtc = now.AddDays(-8)
            },
            new Candidate
            {
                UploadedByUserId = SeedUserId,
                FullName = "Mark Santos",
                Email = "mark.santos@example.com",
                ResumeFileName = "mark_santos_resume.txt",
                ResumeContentType = "text/plain",
                ResumeText = """
                             Machine learning engineer focused on NLP, TF-IDF feature pipelines, model training, evaluation, and ranking systems.
                             Built production inference APIs with Python and worked on ML.NET experimentation in C# environments.
                             """,
                CreatedUtc = now.AddDays(-7)
            },
            new Candidate
            {
                UploadedByUserId = SeedUserId,
                FullName = "Nina Patel",
                Email = "nina.patel@example.com",
                ResumeFileName = "nina_patel_resume.txt",
                ResumeContentType = "text/plain",
                ResumeText = """
                             Frontend developer specializing in Blazor and MudBlazor. Built responsive SaaS dashboards with complex tables, filters, and forms.
                             Strong in C#, HTML, CSS, and API integration.
                             """,
                CreatedUtc = now.AddDays(-6)
            },
            new Candidate
            {
                UploadedByUserId = SeedUserId,
                FullName = "Diego Ramirez",
                Email = "diego.ramirez@example.com",
                ResumeFileName = "diego_ramirez_resume.txt",
                ResumeContentType = "text/plain",
                ResumeText = """
                             Technical recruiter with 5 years of experience in sourcing engineering candidates, ATS workflow optimization, and structured interviews.
                             Experienced in boolean search and stakeholder communication.
                             """,
                CreatedUtc = now.AddDays(-5)
            },
            new Candidate
            {
                UploadedByUserId = SeedUserId,
                FullName = "Priya Desai",
                Email = "priya.desai@example.com",
                ResumeFileName = "priya_desai_resume.txt",
                ResumeContentType = "text/plain",
                ResumeText = """
                             Full-stack engineer with .NET, ASP.NET Core, Blazor, SQL Server, and Docker experience.
                             Delivered recruiter-facing dashboards and candidate pipeline APIs.
                             """,
                CreatedUtc = now.AddDays(-4)
            },
            new Candidate
            {
                UploadedByUserId = SeedUserId,
                FullName = "Omar Khan",
                Email = "omar.khan@example.com",
                ResumeFileName = "omar_khan_resume.txt",
                ResumeContentType = "text/plain",
                ResumeText = """
                             Data scientist with machine learning and NLP background, focused on text classification and keyword extraction.
                             Experience in Python, model evaluation, and SQL analytics.
                             """,
                CreatedUtc = now.AddDays(-3)
            }
        ];
    }

    private static List<ResumeAnalysis> BuildAnalyses(IReadOnlyList<Candidate> candidates, ModelVersion modelVersion, DateTime now)
    {
        return
        [
            new ResumeAnalysis
            {
                CandidateId = candidates[0].Id,
                ModelVersionId = modelVersion.Id,
                PredictedCategory = "IT",
                ConfidenceScore = 0.93,
                ExtractedSkills = TextProcessing.JoinSkillList(["C#", ".NET", "ASP.NET Core", "EF Core", "SQL Server", "Azure", "Docker"]),
                AnalyzedUtc = now.AddDays(-2)
            },
            new ResumeAnalysis
            {
                CandidateId = candidates[1].Id,
                ModelVersionId = modelVersion.Id,
                PredictedCategory = "Data/ML",
                ConfidenceScore = 0.91,
                ExtractedSkills = TextProcessing.JoinSkillList(["Python", "NLP", "TF-IDF", "ML.NET", "Model Evaluation"]),
                AnalyzedUtc = now.AddDays(-2)
            },
            new ResumeAnalysis
            {
                CandidateId = candidates[2].Id,
                ModelVersionId = modelVersion.Id,
                PredictedCategory = "Frontend",
                ConfidenceScore = 0.89,
                ExtractedSkills = TextProcessing.JoinSkillList(["Blazor", "MudBlazor", "C#", "HTML", "CSS"]),
                AnalyzedUtc = now.AddDays(-2)
            },
            new ResumeAnalysis
            {
                CandidateId = candidates[3].Id,
                ModelVersionId = modelVersion.Id,
                PredictedCategory = "HR",
                ConfidenceScore = 0.9,
                ExtractedSkills = TextProcessing.JoinSkillList(["Sourcing", "Interviewing", "ATS", "Boolean Search", "Communication"]),
                AnalyzedUtc = now.AddDays(-2)
            },
            new ResumeAnalysis
            {
                CandidateId = candidates[4].Id,
                ModelVersionId = modelVersion.Id,
                PredictedCategory = "IT",
                ConfidenceScore = 0.84,
                ExtractedSkills = TextProcessing.JoinSkillList([".NET", "ASP.NET Core", "Blazor", "SQL Server", "Docker"]),
                AnalyzedUtc = now.AddDays(-1)
            },
            new ResumeAnalysis
            {
                CandidateId = candidates[5].Id,
                ModelVersionId = modelVersion.Id,
                PredictedCategory = "Data/ML",
                ConfidenceScore = 0.87,
                ExtractedSkills = TextProcessing.JoinSkillList(["Machine Learning", "NLP", "Text Classification", "Python", "SQL"]),
                AnalyzedUtc = now.AddDays(-1)
            }
        ];
    }

    private static List<MatchResult> BuildMatches(
        IReadOnlyList<JobPosting> jobs,
        IReadOnlyList<Candidate> candidates,
        IReadOnlyList<ResumeAnalysis> analyses,
        DateTime now)
    {
        var analysisByCandidateId = analyses.ToDictionary(x => x.CandidateId);
        var results = new List<MatchResult>();

        foreach (var job in jobs)
        {
            var requiredSkills = TextProcessing.ParseSkillList(job.RequiredSkills);
            var optionalSkills = TextProcessing.ParseSkillList(job.OptionalSkills);
            var jobKeywords = TextProcessing.TokenizeKeywords($"{job.Title} {job.Description} {job.RequiredSkills} {job.OptionalSkills}");

            foreach (var candidate in candidates)
            {
                var resumeNormalized = TextProcessing.NormalizeText(candidate.ResumeText);
                var matchedRequired = requiredSkills
                    .Where(skill => ContainsSkill(resumeNormalized, skill))
                    .ToArray();
                var missingRequired = requiredSkills
                    .Except(matchedRequired, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var matchedOptional = optionalSkills
                    .Where(skill => ContainsSkill(resumeNormalized, skill))
                    .ToArray();

                var resumeKeywords = TextProcessing.TokenizeKeywords(candidate.ResumeText);
                var overlappingKeywords = jobKeywords
                    .Intersect(resumeKeywords, StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToArray();

                var requiredCoverage = requiredSkills.Count == 0
                    ? 1.0
                    : (double)matchedRequired.Length / requiredSkills.Count;
                var optionalCoverage = optionalSkills.Count == 0
                    ? 0.0
                    : (double)matchedOptional.Length / optionalSkills.Count;
                var keywordOverlap = jobKeywords.Count == 0
                    ? 0.0
                    : (double)overlappingKeywords.Length / Math.Min(12, jobKeywords.Count);

                var score = Math.Clamp((requiredCoverage * 70d) + (optionalCoverage * 20d) + (keywordOverlap * 10d), 0d, 100d);

                if (score < 45d)
                {
                    continue;
                }

                var analysis = analysisByCandidateId[candidate.Id];

                results.Add(new MatchResult
                {
                    JobPostingId = job.Id,
                    CandidateId = candidate.Id,
                    ResumeAnalysisId = analysis.Id,
                    MatchScore = Math.Round(score, 1),
                    MatchedSkills = string.Join(", ", matchedRequired.Concat(matchedOptional).Distinct(StringComparer.OrdinalIgnoreCase)),
                    MissingSkills = string.Join(", ", missingRequired),
                    TopOverlappingKeywords = string.Join(", ", overlappingKeywords),
                    GeneratedUtc = now.AddDays(-1)
                });
            }
        }

        return results
            .OrderByDescending(x => x.MatchScore)
            .Take(20)
            .ToList();
    }

    private static bool ContainsSkill(string normalizedResumeText, string skill)
    {
        var normalizedSkill = TextProcessing.NormalizeText(skill);
        return normalizedResumeText.Contains(normalizedSkill, StringComparison.Ordinal);
    }
}
