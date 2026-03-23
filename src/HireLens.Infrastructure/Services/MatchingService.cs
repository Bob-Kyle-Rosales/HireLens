using System.Text.RegularExpressions;
using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using HireLens.Domain.Entities;
using HireLens.Infrastructure.Helpers;
using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Services;

internal sealed partial class MatchingService(
    HireLensDbContext dbContext,
    ILogger<MatchingService> logger) : IMatchingService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and",
        "the",
        "with",
        "for",
        "you",
        "your",
        "from",
        "that",
        "this",
        "have",
        "has",
        "are",
        "was",
        "were",
        "into",
        "our",
        "their",
        "they",
        "them",
        "who",
        "how",
        "what",
        "when",
        "where",
        "about"
    };

    private readonly HireLensDbContext _dbContext = dbContext;
    private readonly ILogger<MatchingService> _logger = logger;

    public async Task<MatchResultDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.MatchResults
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return row is null ? null : MapToDto(row);
    }

    public async Task<IReadOnlyList<MatchResultDto>> GetByJobPostingIdAsync(Guid jobPostingId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.MatchResults
            .AsNoTracking()
            .Where(x => x.JobPostingId == jobPostingId)
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.GeneratedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToDto).ToList();
    }

    public async Task<PagedResult<MatchResultDto>> GetByJobPostingIdPagedAsync(
        Guid jobPostingId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (validatedPageNumber, validatedPageSize) = ValidatePaging(pageNumber, pageSize);

        var query = _dbContext.MatchResults
            .AsNoTracking()
            .Where(x => x.JobPostingId == jobPostingId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.GeneratedUtc)
            .Skip((validatedPageNumber - 1) * validatedPageSize)
            .Take(validatedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MatchResultDto>(
            rows.Select(MapToDto).ToList(),
            validatedPageNumber,
            validatedPageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<MatchResultDto>> MatchForJobAsync(
        MatchCandidatesRequest request,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.JobPostingId, cancellationToken)
            ?? throw new InvalidOperationException($"Job posting {request.JobPostingId} was not found.");

        var candidatesQuery = _dbContext.Candidates.AsNoTracking();
        if (request.CandidateIds.Count > 0)
        {
            candidatesQuery = candidatesQuery.Where(x => request.CandidateIds.Contains(x.Id));
        }

        var candidates = await candidatesQuery.ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var candidateIds = candidates.Select(x => x.Id).ToList();
        var latestAnalyses = await _dbContext.ResumeAnalyses
            .AsNoTracking()
            .Where(x => candidateIds.Contains(x.CandidateId))
            .OrderByDescending(x => x.AnalyzedUtc)
            .ToListAsync(cancellationToken);

        var analysisByCandidateId = latestAnalyses
            .GroupBy(x => x.CandidateId)
            .ToDictionary(x => x.Key, x => x.First());

        var existingRows = await _dbContext.MatchResults
            .Where(x => x.JobPostingId == job.Id && candidateIds.Contains(x.CandidateId))
            .ToListAsync(cancellationToken);

        var existingByCandidateId = existingRows.ToDictionary(x => x.CandidateId);

        var requiredSkills = TextProcessing.ParseSkillList(job.RequiredSkills);
        var optionalSkills = TextProcessing.ParseSkillList(job.OptionalSkills);
        var jobDocument = $"{job.Title} {job.Description} {job.RequiredSkills} {job.OptionalSkills}";
        var jobKeywords = TextProcessing.TokenizeKeywords(jobDocument);
        var candidateProfiles = candidates.ToDictionary(
            x => x.Id,
            x => ResumeScoringTextBuilder.Build(x.ResumeText));

        var documents = candidateProfiles.Values.Select(x => x.SimilarityText).Append(jobDocument).ToList();
        var idf = BuildInverseDocumentFrequencies(documents);
        var jobVector = BuildTfIdfVector(jobDocument, idf);
        var generatedUtc = DateTime.UtcNow;

        var results = new List<MatchResult>(capacity: candidates.Count);
        foreach (var candidate in candidates)
        {
            var profile = candidateProfiles[candidate.Id];
            var resumeVector = BuildTfIdfVector(profile.SimilarityText, idf);
            var cosineSimilarity = CosineSimilarity(jobVector, resumeVector);

            var normalizedResume = TextProcessing.NormalizeText(profile.SkillEvidenceText);
            var matchedRequired = requiredSkills.Where(skill => ContainsSkill(normalizedResume, skill)).ToList();
            var missingRequired = requiredSkills.Except(matchedRequired, StringComparer.OrdinalIgnoreCase).ToList();
            var matchedOptional = optionalSkills.Where(skill => ContainsSkill(normalizedResume, skill)).ToList();

            var requiredCoverage = requiredSkills.Count == 0 ? 1d : (double)matchedRequired.Count / requiredSkills.Count;
            var optionalCoverage = optionalSkills.Count == 0 ? 0d : (double)matchedOptional.Count / optionalSkills.Count;
            var missingRatio = requiredSkills.Count == 0 ? 0d : (double)missingRequired.Count / requiredSkills.Count;

            var resumeKeywords = TextProcessing.TokenizeKeywords(profile.SimilarityText);
            var overlappingKeywords = jobKeywords
                .Intersect(resumeKeywords, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(keyword => idf.TryGetValue(keyword, out var weight) ? weight : 0d)
                .Take(8)
                .ToList();

            var score = Math.Clamp(
                (cosineSimilarity * 60d)
                + (requiredCoverage * 30d)
                + (optionalCoverage * 10d)
                - (missingRatio * 15d),
                0d,
                100d);

            var hasExisting = existingByCandidateId.TryGetValue(candidate.Id, out var row);
            if (!hasExisting)
            {
                row = new MatchResult
                {
                    JobPostingId = job.Id,
                    CandidateId = candidate.Id
                };
                _dbContext.MatchResults.Add(row);
            }

            row!.ResumeAnalysisId = analysisByCandidateId.TryGetValue(candidate.Id, out var analysis) ? analysis.Id : null;
            row.MatchScore = Math.Round(score, 1);
            row.MatchedSkills = string.Join(", ", matchedRequired.Concat(matchedOptional).Distinct(StringComparer.OrdinalIgnoreCase));
            row.MissingSkills = string.Join(", ", missingRequired);
            row.TopOverlappingKeywords = string.Join(", ", overlappingKeywords);
            row.GeneratedUtc = generatedUtc;
            results.Add(row);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Generated {Count} match results for job {JobPostingId}", results.Count, job.Id);

        return results
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.GeneratedUtc)
            .Select(MapToDto)
            .ToList();
    }

    private static bool ContainsSkill(string normalizedResume, string skill)
    {
        var normalizedSkill = TextProcessing.NormalizeText(skill);
        return normalizedResume.Contains(normalizedSkill, StringComparison.Ordinal);
    }

    private static Dictionary<string, double> BuildInverseDocumentFrequencies(IEnumerable<string> documents)
    {
        var tokenizedDocuments = documents
            .Select(TokenizeForVector)
            .Where(tokens => tokens.Count > 0)
            .ToList();

        var documentCount = tokenizedDocuments.Count;
        if (documentCount == 0)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        var documentFrequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var tokens in tokenizedDocuments)
        {
            foreach (var term in tokens.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!documentFrequencies.TryAdd(term, 1))
                {
                    documentFrequencies[term]++;
                }
            }
        }

        return documentFrequencies.ToDictionary(
            pair => pair.Key,
            pair => Math.Log((1d + documentCount) / (1d + pair.Value)) + 1d,
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, double> BuildTfIdfVector(
        string text,
        IReadOnlyDictionary<string, double> idf)
    {
        var tokens = TokenizeForVector(text);
        if (tokens.Count == 0)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        var totalTerms = tokens.Count;
        var termCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in tokens)
        {
            if (!termCounts.TryAdd(term, 1))
            {
                termCounts[term]++;
            }
        }

        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (term, count) in termCounts)
        {
            var tf = count / (double)totalTerms;
            var idfWeight = idf.TryGetValue(term, out var weight) ? weight : 1d;
            vector[term] = tf * idfWeight;
        }

        return vector;
    }

    private static double CosineSimilarity(
        IReadOnlyDictionary<string, double> leftVector,
        IReadOnlyDictionary<string, double> rightVector)
    {
        if (leftVector.Count == 0 || rightVector.Count == 0)
        {
            return 0d;
        }

        var smaller = leftVector.Count <= rightVector.Count ? leftVector : rightVector;
        var larger = ReferenceEquals(smaller, leftVector) ? rightVector : leftVector;

        var dotProduct = 0d;
        foreach (var (term, value) in smaller)
        {
            if (larger.TryGetValue(term, out var other))
            {
                dotProduct += value * other;
            }
        }

        var leftNorm = Math.Sqrt(leftVector.Values.Sum(x => x * x));
        var rightNorm = Math.Sqrt(rightVector.Values.Sum(x => x * x));
        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0d;
        }

        return dotProduct / (leftNorm * rightNorm);
    }

    private static List<string> TokenizeForVector(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = TextProcessing.NormalizeText(text);
        return TokenRegex()
            .Matches(normalized)
            .Select(match => match.Value.Trim())
            .Where(token => token.Length > 2 && !StopWords.Contains(token))
            .ToList();
    }

    private static MatchResultDto MapToDto(MatchResult row)
    {
        return new MatchResultDto(
            row.Id,
            row.JobPostingId,
            row.CandidateId,
            row.ResumeAnalysisId,
            row.MatchScore,
            TextProcessing.ParseSkillList(row.MatchedSkills),
            TextProcessing.ParseSkillList(row.MissingSkills),
            TextProcessing.ParseSkillList(row.TopOverlappingKeywords),
            row.GeneratedUtc);
    }

    [GeneratedRegex(@"[a-z0-9\+#\.-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    private static (int PageNumber, int PageSize) ValidatePaging(int pageNumber, int pageSize)
    {
        var validatedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var validatedPageSize = pageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => pageSize
        };

        return (validatedPageNumber, validatedPageSize);
    }
}
