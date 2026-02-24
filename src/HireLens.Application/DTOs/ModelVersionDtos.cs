using System.ComponentModel.DataAnnotations;

namespace HireLens.Application.DTOs;

public sealed record ModelVersionDto(
    Guid Id,
    string Version,
    string ModelType,
    string StoragePath,
    double Accuracy,
    int TrainingSampleCount,
    int TrainingCategoryCount,
    string TrainingCategoryDistribution,
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

    [Range(0, int.MaxValue)]
    public int TrainingSampleCount { get; set; }

    [Range(0, int.MaxValue)]
    public int TrainingCategoryCount { get; set; }

    [StringLength(4000)]
    public string TrainingCategoryDistribution { get; set; } = "{}";

    public bool IsActive { get; set; }
}
