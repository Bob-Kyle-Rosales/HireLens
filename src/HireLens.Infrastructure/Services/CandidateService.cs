using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using HireLens.Domain.Entities;
using HireLens.Domain.Enums;
using HireLens.Infrastructure.Helpers;
using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Services;

internal sealed class CandidateService(
    HireLensDbContext dbContext,
    IResumeTextExtractor resumeTextExtractor,
    IResumeAnalysisService resumeAnalysisService,
    IMatchingService matchingService,
    ILogger<CandidateService> logger) : ICandidateService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".txt"
    };

    private readonly HireLensDbContext _dbContext = dbContext;
    private readonly IResumeTextExtractor _resumeTextExtractor = resumeTextExtractor;
    private readonly IResumeAnalysisService _resumeAnalysisService = resumeAnalysisService;
    private readonly IMatchingService _matchingService = matchingService;
    private readonly ILogger<CandidateService> _logger = logger;

    public async Task<IReadOnlyList<CandidateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _dbContext.Candidates
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        var latestApplications = await GetLatestApplicationInfoAsync(candidates.Select(x => x.Id).ToList(), cancellationToken);
        var latestAnalyses = await GetLatestAnalysisInfoAsync(candidates.Select(x => x.Id).ToList(), cancellationToken);
        return candidates
            .Select(candidate => MapToDto(
                candidate,
                latestApplications.TryGetValue(candidate.Id, out var applicationInfo) ? applicationInfo : null,
                latestAnalyses.TryGetValue(candidate.Id, out var analysisInfo) ? analysisInfo : null))
            .ToList();
    }

    public async Task<PagedResult<CandidateDto>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var (validatedPageNumber, validatedPageSize) = ValidatePaging(pageNumber, pageSize);

        var query = _dbContext.Candidates
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(x =>
                x.FullName.Contains(normalizedSearch) ||
                x.Email.Contains(normalizedSearch) ||
                x.ResumeFileName.Contains(normalizedSearch) ||
                x.ResumeText.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var candidates = await query
            .OrderByDescending(x => x.CreatedUtc)
            .Skip((validatedPageNumber - 1) * validatedPageSize)
            .Take(validatedPageSize)
            .ToListAsync(cancellationToken);

        var candidateIds = candidates.Select(x => x.Id).ToList();
        var latestApplications = await GetLatestApplicationInfoAsync(candidateIds, cancellationToken);
        var latestAnalyses = await GetLatestAnalysisInfoAsync(candidateIds, cancellationToken);

        var items = candidates
            .Select(candidate => MapToDto(
                candidate,
                latestApplications.TryGetValue(candidate.Id, out var applicationInfo) ? applicationInfo : null,
                latestAnalyses.TryGetValue(candidate.Id, out var analysisInfo) ? analysisInfo : null))
            .ToList();

        return new PagedResult<CandidateDto>(items, validatedPageNumber, validatedPageSize, totalCount);
    }

    public async Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var candidate = await _dbContext.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        var latestApplications = await GetLatestApplicationInfoAsync([candidate.Id], cancellationToken);
        var latestAnalyses = await GetLatestAnalysisInfoAsync([candidate.Id], cancellationToken);

        return MapToDto(
            candidate,
            latestApplications.TryGetValue(candidate.Id, out var applicationInfo) ? applicationInfo : null,
            latestAnalyses.TryGetValue(candidate.Id, out var analysisInfo) ? analysisInfo : null);
    }

    public async Task<CandidateDto> UploadAsync(CandidateUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.JobPostingId == Guid.Empty)
        {
            throw new InvalidOperationException("Job posting is required.");
        }

        if (request.ResumeContent.Length == 0)
        {
            throw new InvalidOperationException("Resume file cannot be empty.");
        }

        var sanitizedFileName = Path.GetFileName(request.ResumeFileName).Trim();
        if (string.IsNullOrWhiteSpace(sanitizedFileName))
        {
            throw new InvalidOperationException("Resume file name is invalid.");
        }

        var extension = Path.GetExtension(sanitizedFileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Unsupported file format. Please upload a PDF or text file.");
        }

        var normalizedContentType = string.IsNullOrWhiteSpace(request.ResumeContentType)
            ? "application/octet-stream"
            : request.ResumeContentType.Trim();

        var job = await _dbContext.JobPostings
            .AsNoTracking()
            .Where(x => x.Id == request.JobPostingId)
            .Select(x => new { x.Id, x.Title })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Job posting {request.JobPostingId} was not found.");

        var resumeText = await _resumeTextExtractor.ExtractAsync(
            request.ResumeContent,
            sanitizedFileName,
            normalizedContentType,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(resumeText))
        {
            throw new InvalidOperationException("No readable text could be extracted from the uploaded resume.");
        }

        var now = DateTime.UtcNow;
        var candidate = new Candidate
        {
            UploadedByUserId = request.UploadedByUserId,
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            ResumeFileName = sanitizedFileName,
            ResumeContentType = normalizedContentType,
            ResumeText = resumeText,
            CreatedUtc = now
        };

        var application = new JobApplication
        {
            JobPostingId = request.JobPostingId,
            CandidateId = candidate.Id,
            Status = ApplicationStatus.Submitted,
            AppliedUtc = now,
            UpdatedUtc = now
        };

        _dbContext.Candidates.Add(candidate);
        _dbContext.JobApplications.Add(application);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _resumeAnalysisService.AnalyzeCandidateAsync(candidate.Id, cancellationToken);
            application.Status = ApplicationStatus.Analyzed;
            application.UpdatedUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _matchingService.MatchForJobAsync(
                new MatchCandidatesRequest
                {
                    JobPostingId = request.JobPostingId,
                    CandidateIds = [candidate.Id]
                },
                cancellationToken);

            application.Status = ApplicationStatus.Scored;
            application.UpdatedUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            application.Status = ApplicationStatus.Failed;
            application.UpdatedUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                ex,
                "Candidate {CandidateId} uploaded but automatic processing failed for job {JobPostingId}",
                candidate.Id,
                request.JobPostingId);
        }

        _logger.LogInformation(
            "Candidate {CandidateId} uploaded and applied to job {JobPostingId}",
            candidate.Id,
            request.JobPostingId);

        var applicationInfo = new CandidateApplicationInfo(
            job.Id,
            job.Title,
            application.Status.ToString(),
            application.AppliedUtc);

        var latestAnalyses = await GetLatestAnalysisInfoAsync([candidate.Id], cancellationToken);

        return MapToDto(
            candidate,
            applicationInfo,
            latestAnalyses.TryGetValue(candidate.Id, out var analysisInfo) ? analysisInfo : null);
    }

    private async Task<Dictionary<Guid, CandidateApplicationInfo>> GetLatestApplicationInfoAsync(
        IReadOnlyList<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var latestApplications = await _dbContext.JobApplications
            .AsNoTracking()
            .Where(x => candidateIds.Contains(x.CandidateId))
            .OrderByDescending(x => x.AppliedUtc)
            .ToListAsync(cancellationToken);

        if (latestApplications.Count == 0)
        {
            return [];
        }

        var latestByCandidateId = latestApplications
            .GroupBy(x => x.CandidateId)
            .Select(x => x.First())
            .ToList();

        var jobIds = latestByCandidateId
            .Select(x => x.JobPostingId)
            .Distinct()
            .ToList();

        var jobTitles = await _dbContext.JobPostings
            .AsNoTracking()
            .Where(x => jobIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

        var result = new Dictionary<Guid, CandidateApplicationInfo>();
        foreach (var application in latestByCandidateId)
        {
            jobTitles.TryGetValue(application.JobPostingId, out var jobTitle);
            result[application.CandidateId] = new CandidateApplicationInfo(
                application.JobPostingId,
                jobTitle ?? "Unknown role",
                application.Status.ToString(),
                application.AppliedUtc);
        }

        return result;
    }

    private async Task<Dictionary<Guid, CandidateAnalysisInfo>> GetLatestAnalysisInfoAsync(
        IReadOnlyList<Guid> candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var latestAnalyses = await _dbContext.ResumeAnalyses
            .AsNoTracking()
            .Where(x => candidateIds.Contains(x.CandidateId))
            .OrderByDescending(x => x.AnalyzedUtc)
            .ToListAsync(cancellationToken);

        if (latestAnalyses.Count == 0)
        {
            return [];
        }

        return latestAnalyses
            .GroupBy(x => x.CandidateId)
            .Select(x => x.First())
            .ToDictionary(
                x => x.CandidateId,
                x => new CandidateAnalysisInfo(
                    x.PredictedCategory,
                    x.ConfidenceScore,
                    TextProcessing.ParseSkillList(x.ExtractedSkills),
                    x.AnalyzedUtc));
    }

    private static CandidateDto MapToDto(
        Candidate candidate,
        CandidateApplicationInfo? applicationInfo,
        CandidateAnalysisInfo? analysisInfo)
    {
        return new CandidateDto(
            candidate.Id,
            candidate.FullName,
            candidate.Email,
            candidate.ResumeFileName,
            candidate.CreatedUtc,
            applicationInfo?.JobPostingId,
            applicationInfo?.JobTitle,
            applicationInfo?.Status,
            applicationInfo?.AppliedUtc,
            analysisInfo?.PredictedCategory,
            analysisInfo?.ConfidenceScore,
            analysisInfo?.ExtractedSkills ?? [],
            analysisInfo?.AnalyzedUtc);
    }

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

    private sealed record CandidateApplicationInfo(
        Guid JobPostingId,
        string JobTitle,
        string Status,
        DateTime AppliedUtc);

    private sealed record CandidateAnalysisInfo(
        string PredictedCategory,
        double ConfidenceScore,
        IReadOnlyList<string> ExtractedSkills,
        DateTime AnalyzedUtc);
}
