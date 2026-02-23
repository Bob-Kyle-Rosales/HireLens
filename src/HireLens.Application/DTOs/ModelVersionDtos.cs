using System.ComponentModel.DataAnnotations;

namespace HireLens.Application.DTOs;

public sealed record ModelVersionDto(
    Guid Id,
    string Version,
    string ModelType,
    string StoragePath,
    double Accuracy,
    bool IsActive,
    DateTime TrainedUtc);

public sealed class CreateModelVersionRequest
{
    [Required]
    [StringLength(100)]
    public string Version { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ModelType { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string StoragePath { get; set; } = string.Empty;

    [Range(0, 1)]
    public double Accuracy { get; set; }

    public bool IsActive { get; set; }
}
