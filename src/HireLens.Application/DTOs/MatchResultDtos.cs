using System.ComponentModel.DataAnnotations;

namespace HireLens.Application.DTOs;

public sealed record MatchResultDto(
    Guid Id,
    Guid JobPostingId,
    Guid CandidateId,
    Guid? ResumeAnalysisId,
    double MatchScore,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> TopOverlappingKeywords,
    DateTime GeneratedUtc);

public sealed class MatchCandidatesRequest
{
    [Required]
    public Guid JobPostingId { get; set; }

    public List<Guid> CandidateIds { get; set; } = [];
}
