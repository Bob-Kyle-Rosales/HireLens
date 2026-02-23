using HireLens.Application.DTOs;

namespace HireLens.Application.Interfaces;

public interface IMatchingService
{
    Task<MatchResultDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchResultDto>> GetByJobPostingIdAsync(Guid jobPostingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MatchResultDto>> MatchForJobAsync(MatchCandidatesRequest request, CancellationToken cancellationToken = default);
}
