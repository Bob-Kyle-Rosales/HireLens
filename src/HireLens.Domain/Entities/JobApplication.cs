using HireLens.Domain.Enums;

namespace HireLens.Domain.Entities;

public class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobPostingId { get; set; }
    public Guid CandidateId { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
