using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
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

        var hasJobs = await dbContext.JobPostings.AnyAsync();
        var hasCandidates = await dbContext.Candidates.AnyAsync();

        if (hasJobs && hasCandidates)
        {
            await BackfillApplicationFlowDataAsync(scope.ServiceProvider, dbContext, logger);
            return;
        }

        if (hasJobs || hasCandidates)
        {
            logger.LogWarning("Seed data skipped because only partial core data exists (jobs={HasJobs}, candidates={HasCandidates}).", hasJobs, hasCandidates);
            return;
        }

        var now = DateTime.UtcNow;

        var modelVersion = new ModelVersion
        {
            Version = "resume-category-v1",
            ModelType = "ResumeCategoryClassifier",
            StoragePath = "models/resume-category-v1.zip",
            Accuracy = 0.84,
            TrainingSampleCount = 6,
            TrainingCategoryCount = 4,
            TrainingCategoryDistribution = "{\"IT\":2,\"Data/ML\":2,\"Frontend\":1,\"HR\":1}",
            IsActive = true,
            TrainedUtc = now.AddDays(-3)
        };

        var jobs = BuildJobs(now);
        var candidates = BuildCandidates(now);
        var analyses = BuildAnalyses(candidates, modelVersion, now);
        var applications = BuildApplications(jobs, candidates, now);
        var matches = BuildMatches(jobs, candidates, analyses, applications, now);

        dbContext.ModelVersions.Add(modelVersion);
        dbContext.JobPostings.AddRange(jobs);
        dbContext.Candidates.AddRange(candidates);
        dbContext.JobApplications.AddRange(applications);
        dbContext.ResumeAnalyses.AddRange(analyses);
        dbContext.MatchResults.AddRange(matches);

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Seeded initial data: {Jobs} jobs, {Candidates} candidates, {Applications} applications, {Analyses} analyses, {Matches} matches.",
            jobs.Count,
            candidates.Count,
            applications.Count,
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

    private static List<JobApplication> BuildApplications(
        IReadOnlyList<JobPosting> jobs,
        IReadOnlyList<Candidate> candidates,
        DateTime now)
    {
        return
        [
            new JobApplication
            {
                JobPostingId = jobs[0].Id,
                CandidateId = candidates[0].Id,
                Status = ApplicationStatus.Scored,
                AppliedUtc = now.AddDays(-8),
                UpdatedUtc = now.AddDays(-1)
            },
            new JobApplication
            {
                JobPostingId = jobs[1].Id,
                CandidateId = candidates[1].Id,
                Status = ApplicationStatus.Scored,
                AppliedUtc = now.AddDays(-7),
                UpdatedUtc = now.AddDays(-1)
            },
            new JobApplication
            {
                JobPostingId = jobs[2].Id,
                CandidateId = candidates[2].Id,
                Status = ApplicationStatus.Scored,
                AppliedUtc = now.AddDays(-6),
                UpdatedUtc = now.AddDays(-1)
            },
            new JobApplication
            {
                JobPostingId = jobs[3].Id,
                CandidateId = candidates[3].Id,
                Status = ApplicationStatus.Scored,
                AppliedUtc = now.AddDays(-5),
                UpdatedUtc = now.AddDays(-1)
            },
            new JobApplication
            {
                JobPostingId = jobs[0].Id,
                CandidateId = candidates[4].Id,
                Status = ApplicationStatus.Scored,
                AppliedUtc = now.AddDays(-4),
                UpdatedUtc = now.AddDays(-1)
            },
            new JobApplication
            {
                JobPostingId = jobs[1].Id,
                CandidateId = candidates[5].Id,
                Status = ApplicationStatus.Scored,
                AppliedUtc = now.AddDays(-3),
                UpdatedUtc = now.AddDays(-1)
            }
        ];
    }

    private static List<MatchResult> BuildMatches(
        IReadOnlyList<JobPosting> jobs,
        IReadOnlyList<Candidate> candidates,
        IReadOnlyList<ResumeAnalysis> analyses,
        IReadOnlyList<JobApplication> applications,
        DateTime now)
    {
        var jobsById = jobs.ToDictionary(x => x.Id);
        var candidatesById = candidates.ToDictionary(x => x.Id);
        var analysisByCandidateId = analyses.ToDictionary(x => x.CandidateId);
        var results = new List<MatchResult>();

        foreach (var application in applications)
        {
            if (!jobsById.TryGetValue(application.JobPostingId, out var job) ||
                !candidatesById.TryGetValue(application.CandidateId, out var candidate))
            {
                continue;
            }

            var requiredSkills = TextProcessing.ParseSkillList(job.RequiredSkills);
            var optionalSkills = TextProcessing.ParseSkillList(job.OptionalSkills);
            var jobKeywords = TextProcessing.TokenizeKeywords($"{job.Title} {job.Description} {job.RequiredSkills} {job.OptionalSkills}");

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

    private static async Task BackfillApplicationFlowDataAsync(
        IServiceProvider services,
        HireLensDbContext dbContext,
        ILogger logger)
    {
        var jobs = await dbContext.JobPostings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedUtc)
            .Select(x => new { x.Id, x.Title })
            .ToListAsync();

        var candidates = await dbContext.Candidates
            .AsNoTracking()
            .OrderBy(x => x.CreatedUtc)
            .Select(x => new { x.Id })
            .ToListAsync();

        if (jobs.Count == 0 || candidates.Count == 0)
        {
            logger.LogInformation("Application flow backfill skipped because jobs or candidates are missing.");
            return;
        }

        var existingApplications = await dbContext.JobApplications
            .AsNoTracking()
            .Select(x => new { x.CandidateId })
            .ToListAsync();

        var candidateIdsWithApplication = existingApplications
            .Select(x => x.CandidateId)
            .ToHashSet();

        var candidatesWithoutApplication = candidates
            .Where(x => !candidateIdsWithApplication.Contains(x.Id))
            .ToList();

        if (candidatesWithoutApplication.Count > 0)
        {
            var newApplications = new List<JobApplication>(candidatesWithoutApplication.Count);
            for (var i = 0; i < candidatesWithoutApplication.Count; i++)
            {
                var candidate = candidatesWithoutApplication[i];
                var assignedJob = jobs[i % jobs.Count];
                var now = DateTime.UtcNow;

                newApplications.Add(new JobApplication
                {
                    JobPostingId = assignedJob.Id,
                    CandidateId = candidate.Id,
                    Status = ApplicationStatus.Submitted,
                    AppliedUtc = now,
                    UpdatedUtc = now
                });
            }

            dbContext.JobApplications.AddRange(newApplications);
            await dbContext.SaveChangesAsync();

            logger.LogInformation(
                "Backfilled {Count} job applications for candidates missing application records.",
                newApplications.Count);
        }

        var matchingService = services.GetRequiredService<IMatchingService>();
        var resumeAnalysisService = services.GetRequiredService<IResumeAnalysisService>();

        var applications = await dbContext.JobApplications
            .OrderBy(x => x.AppliedUtc)
            .ToListAsync();

        var scoredCount = 0;
        var failedCount = 0;

        foreach (var application in applications)
        {
            var hasMatch = await dbContext.MatchResults
                .AsNoTracking()
                .AnyAsync(x => x.JobPostingId == application.JobPostingId && x.CandidateId == application.CandidateId);

            if (hasMatch)
            {
                if (application.Status != ApplicationStatus.Scored)
                {
                    application.Status = ApplicationStatus.Scored;
                    application.UpdatedUtc = DateTime.UtcNow;
                }

                scoredCount++;
                continue;
            }

            try
            {
                await resumeAnalysisService.AnalyzeCandidateAsync(application.CandidateId);
                application.Status = ApplicationStatus.Analyzed;
                application.UpdatedUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();

                await matchingService.MatchForJobAsync(
                    new MatchCandidatesRequest
                    {
                        JobPostingId = application.JobPostingId,
                        CandidateIds = [application.CandidateId]
                    });

                application.Status = ApplicationStatus.Scored;
                application.UpdatedUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                scoredCount++;
            }
            catch (Exception ex)
            {
                application.Status = ApplicationStatus.Failed;
                application.UpdatedUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                failedCount++;

                logger.LogWarning(
                    ex,
                    "Backfill failed for candidate {CandidateId} on job {JobPostingId}.",
                    application.CandidateId,
                    application.JobPostingId);
            }
        }

        logger.LogInformation(
            "Application flow backfill completed. Scored={ScoredCount}, Failed={FailedCount}.",
            scoredCount,
            failedCount);
    }
}
