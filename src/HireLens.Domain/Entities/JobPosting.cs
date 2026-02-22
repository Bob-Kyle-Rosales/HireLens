using HireLens.Domain.Enums;

namespace HireLens.Domain.Entities;

public class JobPosting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequiredSkills { get; set; } = string.Empty;
    public string OptionalSkills { get; set; } = string.Empty;
    public SeniorityLevel SeniorityLevel { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
