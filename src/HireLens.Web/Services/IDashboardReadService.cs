using HireLens.Application.DTOs;

namespace HireLens.Web.Services;

public interface IDashboardReadService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobLookupDto>> GetJobLookupAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchResultRowDto>> GetMatchResultsAsync(Guid? jobPostingId = null, CancellationToken cancellationToken = default);
    Task<PagedResult<MatchResultRowDto>> GetMatchResultsPagedAsync(
        Guid? jobPostingId = null,
        int pageNumber = 1,
        int pageSize = 10,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelVersionRowDto>> GetModelVersionsAsync(CancellationToken cancellationToken = default);
}

public sealed record DashboardSummaryDto(
    int JobCount,
    int CandidateCount,
    int ResumeAnalysisCount,
    int MatchResultCount,
    IReadOnlyList<DashboardJobDto> LatestJobs,
    IReadOnlyList<DashboardCandidateDto> LatestCandidates);

public sealed record DashboardJobDto(
    Guid Id,
    string Title,
    string SeniorityLevel,
    DateTime UpdatedUtc);

public sealed record DashboardCandidateDto(
    Guid Id,
    string FullName,
    string Email,
    DateTime CreatedUtc);

public sealed record JobLookupDto(Guid Id, string Title);

public sealed record MatchResultRowDto(
    Guid Id,
    Guid JobPostingId,
    string JobTitle,
    Guid CandidateId,
    string CandidateName,
    double MatchScore,
    string MatchedSkills,
    string MissingSkills,
    string TopOverlappingKeywords,
    string PredictedCategory,
    double? ConfidenceScore,
    DateTime GeneratedUtc);

public sealed record ModelVersionRowDto(
    Guid Id,
    string Version,
    string ModelType,
    double Accuracy,
    bool IsActive,
    DateTime TrainedUtc,
    string StoragePath);
