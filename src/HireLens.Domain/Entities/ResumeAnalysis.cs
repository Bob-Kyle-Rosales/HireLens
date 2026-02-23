namespace HireLens.Domain.Entities;

public class ResumeAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public Guid? ModelVersionId { get; set; }
    public string PredictedCategory { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string ExtractedSkills { get; set; } = string.Empty;
    public DateTime AnalyzedUtc { get; set; } = DateTime.UtcNow;
}
