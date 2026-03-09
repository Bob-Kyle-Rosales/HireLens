using HireLens.Application.DTOs;

namespace HireLens.Application.Interfaces;

public interface ICandidateService
{
    Task<IReadOnlyList<CandidateDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<CandidateDto>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CandidateDto> UploadAsync(CandidateUploadRequest request, CancellationToken cancellationToken = default);
}
