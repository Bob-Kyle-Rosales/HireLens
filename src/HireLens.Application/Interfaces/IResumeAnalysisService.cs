using HireLens.Application.DTOs;

namespace HireLens.Application.Interfaces;

public interface IResumeAnalysisService
{
    Task<ResumeAnalysisDto?> GetLatestByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResumeAnalysisDto>> GetByCandidateIdAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<ResumeAnalysisDto> UpsertAsync(ResumeAnalysisUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ResumeAnalysisDto> AnalyzeCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<int> AnalyzeAllCandidatesAsync(CancellationToken cancellationToken = default);
}
