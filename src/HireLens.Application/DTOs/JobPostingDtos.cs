using System.ComponentModel.DataAnnotations;
using HireLens.Domain.Enums;

namespace HireLens.Application.DTOs;

public sealed record JobPostingDto(
    Guid Id,
    string Title,
    string Description,
    IReadOnlyList<string> RequiredSkills,
    IReadOnlyList<string> OptionalSkills,
    SeniorityLevel SeniorityLevel,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed class JobPostingUpsertRequest
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(6000)]
    public string Description { get; set; } = string.Empty;

    public List<string> RequiredSkills { get; set; } = [];
    public List<string> OptionalSkills { get; set; } = [];
    public SeniorityLevel SeniorityLevel { get; set; } = SeniorityLevel.Mid;
}
