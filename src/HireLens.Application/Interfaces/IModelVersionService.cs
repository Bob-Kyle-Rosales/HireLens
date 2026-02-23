using HireLens.Application.DTOs;

namespace HireLens.Application.Interfaces;

public interface IModelVersionService
{
    Task<IReadOnlyList<ModelVersionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ModelVersionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ModelVersionDto?> GetActiveAsync(string modelType, CancellationToken cancellationToken = default);
    Task<ModelVersionDto> CreateAsync(CreateModelVersionRequest request, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(Guid id, CancellationToken cancellationToken = default);
}
