using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using HireLens.Domain.Entities;
using HireLens.Infrastructure.Helpers;
using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace HireLens.Infrastructure.Services;

internal sealed class ResumeAnalysisService(
    HireLensDbContext dbContext,
    IHostEnvironment hostEnvironment,
    ILogger<ResumeAnalysisService> logger) : IResumeAnalysisService
{
    private static readonly string[] KnownSkills =
    [
        "C#",
        ".NET",
        ".NET 8",
        "ASP.NET Core",
        "EF Core",
        "SQL Server",
        "PostgreSQL",
        "Docker",
        "Azure",
        "Redis",
        "Blazor",
        "MudBlazor",
        "JavaScript",
        "HTML",
        "CSS",
        "REST API",
        "Machine Learning",
        "ML.NET",
        "NLP",
        "TF-IDF",
        "Model Evaluation",
        "Sourcing",
        "Interviewing",
        "ATS",
        "Communication",
        "Boolean Search"
    ];

    private static readonly Dictionary<string, string[]> CategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IT"] = [".net", "asp.net", "c#", "api", "backend", "software", "sql"],
        ["Frontend"] = ["blazor", "javascript", "html", "css", "frontend", "ui", "ux"],
        ["Data/ML"] = ["machine learning", "ml", "nlp", "tf-idf", "model", "data scientist", "classification"],
        ["HR"] = ["recruiter", "ats", "sourcing", "interview", "talent", "human resources", "hiring"],
        ["Finance"] = ["finance", "accounting", "audit", "budget", "forecast", "fp&a"]
    };

    private readonly HireLensDbContext _dbContext = dbContext;
    private readonly ILogger<ResumeAnalysisService> _logger = logger;
    private readonly string _contentRootPath = hostEnvironment.ContentRootPath;

    public async Task<ResumeAnalysisDto?> GetLatestByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.ResumeAnalyses
            .AsNoTracking()
            .Where(x => x.CandidateId == candidateId)
            .OrderByDescending(x => x.AnalyzedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : MapToDto(row);
    }

    public async Task<IReadOnlyList<ResumeAnalysisDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.ResumeAnalyses
            .AsNoTracking()
            .Where(x => x.CandidateId == candidateId)
            .OrderByDescending(x => x.AnalyzedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToDto).ToList();
    }

    public async Task<ResumeAnalysisDto> UpsertAsync(ResumeAnalysisUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var latest = await _dbContext.ResumeAnalyses
            .Where(x => x.CandidateId == request.CandidateId && x.ModelVersionId == request.ModelVersionId)
            .OrderByDescending(x => x.AnalyzedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            latest = new ResumeAnalysis
            {
                CandidateId = request.CandidateId,
                ModelVersionId = request.ModelVersionId
            };
            _dbContext.ResumeAnalyses.Add(latest);
        }

        latest.PredictedCategory = request.PredictedCategory.Trim();
        latest.ConfidenceScore = Math.Clamp(request.ConfidenceScore, 0d, 1d);
        latest.ExtractedSkills = TextProcessing.JoinSkillList(request.ExtractedSkills);
        latest.AnalyzedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(latest);
    }

    public async Task<ResumeAnalysisDto> AnalyzeCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default)
    {
        var candidate = await _dbContext.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == candidateId, cancellationToken)
            ?? throw new InvalidOperationException($"Candidate {candidateId} was not found.");

        var extractedSkills = ExtractSkills(candidate.ResumeText);
        var prediction = await PredictCategoryAsync(candidate.ResumeText, cancellationToken);

        var request = new ResumeAnalysisUpsertRequest
        {
            CandidateId = candidateId,
            ModelVersionId = prediction.ModelVersionId,
            PredictedCategory = prediction.Category,
            ConfidenceScore = prediction.ConfidenceScore,
            ExtractedSkills = extractedSkills.ToList()
        };

        var analysis = await UpsertAsync(request, cancellationToken);

        _logger.LogInformation(
            "Resume analyzed for candidate {CandidateId}. Category={Category}, Confidence={Confidence:0.00}",
            candidateId,
            analysis.PredictedCategory,
            analysis.ConfidenceScore);

        return analysis;
    }

    public async Task<int> AnalyzeAllCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var candidateIds = await _dbContext.Candidates
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var analyzed = 0;
        foreach (var candidateId in candidateIds)
        {
            await AnalyzeCandidateAsync(candidateId, cancellationToken);
            analyzed++;
        }

        return analyzed;
    }

    private async Task<CategoryPredictionResult> PredictCategoryAsync(string resumeText, CancellationToken cancellationToken)
    {
        var activeModel = await _dbContext.ModelVersions
            .AsNoTracking()
            .Where(x => x.ModelType == ModelVersionService.ResumeCategoryModelType && x.IsActive)
            .OrderByDescending(x => x.TrainedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeModel is null)
        {
            return PredictWithHeuristics(resumeText);
        }

        var absolutePath = ResolveAbsolutePath(activeModel.StoragePath);
        if (!File.Exists(absolutePath))
        {
            _logger.LogWarning("Active model file was not found at {Path}. Falling back to keyword classifier.", absolutePath);
            return PredictWithHeuristics(resumeText);
        }

        try
        {
            var mlContext = new MLContext(seed: 42);
            var model = mlContext.Model.Load(absolutePath, out _);
            var engine = mlContext.Model.CreatePredictionEngine<ResumeCategoryPredictionInput, ResumeCategoryPredictionOutput>(model);
            var prediction = engine.Predict(new ResumeCategoryPredictionInput { Text = resumeText });

            var label = string.IsNullOrWhiteSpace(prediction.PredictedLabelValue)
                ? "General"
                : prediction.PredictedLabelValue.Trim();

            var confidence = CalculateConfidence(prediction.Score);
            return new CategoryPredictionResult(label, confidence, activeModel.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run ML.NET prediction; using heuristic classifier.");
            return PredictWithHeuristics(resumeText);
        }
    }

    private static CategoryPredictionResult PredictWithHeuristics(string resumeText)
    {
        var normalized = TextProcessing.NormalizeText(resumeText);
        var bestCategory = "General";
        var bestHits = 0;

        foreach (var (category, keywords) in CategoryKeywords)
        {
            var hitCount = keywords.Count(keyword =>
                normalized.Contains(TextProcessing.NormalizeText(keyword), StringComparison.Ordinal));

            if (hitCount > bestHits)
            {
                bestHits = hitCount;
                bestCategory = category;
            }
        }

        var confidence = bestHits == 0 ? 0.40 : Math.Min(0.90, 0.45 + (bestHits * 0.10));
        return new CategoryPredictionResult(bestCategory, confidence, null);
    }

    private static double CalculateConfidence(IReadOnlyList<float>? scores)
    {
        if (scores is null || scores.Count == 0)
        {
            return 0.50;
        }

        var max = scores.Max();
        var expScores = scores.Select(score => Math.Exp(score - max)).ToArray();
        var denominator = expScores.Sum();

        if (denominator <= 0)
        {
            return 0.50;
        }

        var maxProbability = expScores.Max() / denominator;
        return Math.Clamp(maxProbability, 0d, 1d);
    }

    private static IReadOnlyList<string> ExtractSkills(string resumeText)
    {
        var normalized = TextProcessing.NormalizeText(resumeText);
        var skillMatches = KnownSkills
            .Where(skill => normalized.Contains(TextProcessing.NormalizeText(skill), StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (skillMatches.Count > 0)
        {
            return skillMatches;
        }

        return TextProcessing.TokenizeKeywords(resumeText)
            .Take(12)
            .ToList();
    }

    private string ResolveAbsolutePath(string storagePath)
    {
        if (Path.IsPathRooted(storagePath))
        {
            return storagePath;
        }

        var normalized = storagePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(_contentRootPath, normalized));
    }

    private static ResumeAnalysisDto MapToDto(ResumeAnalysis row)
    {
        return new ResumeAnalysisDto(
            row.Id,
            row.CandidateId,
            row.ModelVersionId,
            row.PredictedCategory,
            row.ConfidenceScore,
            TextProcessing.ParseSkillList(row.ExtractedSkills),
            row.AnalyzedUtc);
    }

    private sealed class ResumeCategoryPredictionInput
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class ResumeCategoryPredictionOutput
    {
        [ColumnName("PredictedLabelValue")]
        public string PredictedLabelValue { get; set; } = string.Empty;

        public float[] Score { get; set; } = [];
    }

    private sealed record CategoryPredictionResult(
        string Category,
        double ConfidenceScore,
        Guid? ModelVersionId);
}
