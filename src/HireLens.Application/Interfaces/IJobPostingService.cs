using HireLens.Application.DTOs;

namespace HireLens.Application.Interfaces;

public interface IJobPostingService
{
    Task<IReadOnlyList<JobPostingDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<JobPostingDto>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<JobPostingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobPostingDto> CreateAsync(JobPostingUpsertRequest request, string userId, CancellationToken cancellationToken = default);
    Task<JobPostingDto?> UpdateAsync(Guid id, JobPostingUpsertRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
