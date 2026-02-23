using System.ComponentModel.DataAnnotations;

namespace HireLens.Application.DTOs;

public sealed record ResumeAnalysisDto(
    Guid Id,
    Guid CandidateId,
    Guid? ModelVersionId,
    string PredictedCategory,
    double ConfidenceScore,
    IReadOnlyList<string> ExtractedSkills,
    DateTime AnalyzedUtc);

public sealed class ResumeAnalysisUpsertRequest
{
    [Required]
    public Guid CandidateId { get; set; }

    public Guid? ModelVersionId { get; set; }

    [Required]
    [StringLength(120)]
    public string PredictedCategory { get; set; } = string.Empty;

    [Range(0, 1)]
    public double ConfidenceScore { get; set; }

    public List<string> ExtractedSkills { get; set; } = [];
}
