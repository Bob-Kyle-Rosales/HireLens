namespace HireLens.Infrastructure.Configuration;

public sealed class MlOptions
{
    public string ModelDirectory { get; set; } = "models";
    public MlTrainingOptions Training { get; set; } = new();
}

public sealed class MlTrainingOptions
{
    public int MinLabeledResumes { get; set; } = 25;
    public int MinDistinctCategories { get; set; } = 3;
}
