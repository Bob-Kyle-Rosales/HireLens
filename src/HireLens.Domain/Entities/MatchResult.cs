namespace HireLens.Domain.Entities;

public class MatchResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobPostingId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid? ResumeAnalysisId { get; set; }
    public double MatchScore { get; set; }
    public string MatchedSkills { get; set; } = string.Empty;
    public string MissingSkills { get; set; } = string.Empty;
    public string TopOverlappingKeywords { get; set; } = string.Empty;
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
}
