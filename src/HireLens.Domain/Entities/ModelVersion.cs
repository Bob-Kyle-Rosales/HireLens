namespace HireLens.Domain.Entities;

public class ModelVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public double Accuracy { get; set; }
    public bool IsActive { get; set; }
    public DateTime TrainedUtc { get; set; } = DateTime.UtcNow;
}
