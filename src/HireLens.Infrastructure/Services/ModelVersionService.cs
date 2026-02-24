using System.Text.Json;
using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using HireLens.Domain.Entities;
using HireLens.Infrastructure.Configuration;
using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;

namespace HireLens.Infrastructure.Services;

internal sealed class ModelVersionService(
    HireLensDbContext dbContext,
    IOptions<MlOptions> mlOptions,
    IHostEnvironment hostEnvironment,
    ILogger<ModelVersionService> logger) : IModelVersionService
{
    public const string ResumeCategoryModelType = "ResumeCategoryClassifier";

    private const int TrainingSeed = 42;
    private readonly HireLensDbContext _dbContext = dbContext;
    private readonly ILogger<ModelVersionService> _logger = logger;
    private readonly string _contentRootPath = hostEnvironment.ContentRootPath;
    private readonly string _modelDirectory = mlOptions.Value.ModelDirectory;
    private readonly int _minLabeledResumes = mlOptions.Value.Training.MinLabeledResumes;
    private readonly int _minDistinctCategories = mlOptions.Value.Training.MinDistinctCategories;

    public async Task<IReadOnlyList<ModelVersionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.ModelVersions
            .AsNoTracking()
            .OrderByDescending(x => x.TrainedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapToDto).ToList();
    }

    public async Task<ModelVersionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.ModelVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return row is null ? null : MapToDto(row);
    }

    public async Task<ModelVersionDto?> GetActiveAsync(string modelType, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.ModelVersions
            .AsNoTracking()
            .Where(x => x.ModelType == modelType && x.IsActive)
            .OrderByDescending(x => x.TrainedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : MapToDto(row);
    }

    public async Task<ModelVersionDto> CreateAsync(CreateModelVersionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.IsActive)
        {
            await DeactivateActiveVersionsAsync(request.ModelType, cancellationToken);
        }

        var row = new ModelVersion
        {
            Version = request.Version.Trim(),
            ModelType = request.ModelType.Trim(),
            StoragePath = request.StoragePath.Trim(),
            Accuracy = request.Accuracy,
            TrainingSampleCount = request.TrainingSampleCount,
            TrainingCategoryCount = request.TrainingCategoryCount,
            TrainingCategoryDistribution = string.IsNullOrWhiteSpace(request.TrainingCategoryDistribution)
                ? "{}"
                : request.TrainingCategoryDistribution.Trim(),
            IsActive = request.IsActive,
            TrainedUtc = DateTime.UtcNow
        };

        _dbContext.ModelVersions.Add(row);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Model version created: {ModelType}/{Version}", row.ModelType, row.Version);
        return MapToDto(row);
    }

    public async Task<bool> SetActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.ModelVersions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            return false;
        }

        if (!row.IsActive)
        {
            await DeactivateActiveVersionsAsync(row.ModelType, cancellationToken);
            row.IsActive = true;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Model version activated: {ModelType}/{Version}", row.ModelType, row.Version);
        }

        return true;
    }

    public async Task<ModelVersionDto> TrainResumeCategoryModelAsync(CancellationToken cancellationToken = default)
    {
        var sourceRows = await _dbContext.Candidates
            .AsNoTracking()
            .Select(candidate => new
            {
                candidate.ResumeText,
                Label = _dbContext.ResumeAnalyses
                    .Where(analysis => analysis.CandidateId == candidate.Id)
                    .OrderByDescending(analysis => analysis.AnalyzedUtc)
                    .Select(analysis => analysis.PredictedCategory)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var rows = sourceRows
            .Select(x => new ResumeCategoryTrainingInput
            {
                Text = x.ResumeText,
                Label = (x.Label ?? string.Empty).Trim()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Text) && !string.IsNullOrWhiteSpace(x.Label))
            .ToList();

        var categoryCount = rows.Select(x => x.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (rows.Count < _minLabeledResumes || categoryCount < _minDistinctCategories)
        {
            throw new InvalidOperationException(
                $"Training requires at least {_minLabeledResumes} labeled resumes across at least {_minDistinctCategories} categories. " +
                $"Current dataset: {rows.Count} resumes, {categoryCount} categories.");
        }

        var categoryDistribution = rows
            .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        var mlContext = new MLContext(seed: TrainingSeed);
        var dataView = mlContext.Data.LoadFromEnumerable(rows);
        var testFraction = rows.Count >= 10 ? 0.2 : 0.33;
        var split = mlContext.Data.TrainTestSplit(dataView, testFraction: testFraction, seed: TrainingSeed);

        var pipeline = mlContext.Transforms.Conversion.MapValueToKey("LabelKey", nameof(ResumeCategoryTrainingInput.Label))
            .Append(mlContext.Transforms.Text.FeaturizeText("Features", nameof(ResumeCategoryTrainingInput.Text)))
            .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("LabelKey", "Features"))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabelValue", "PredictedLabel"));

        var model = pipeline.Fit(split.TrainSet);
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.MulticlassClassification.Evaluate(
            predictions,
            labelColumnName: "LabelKey",
            predictedLabelColumnName: "PredictedLabel",
            scoreColumnName: "Score");

        var accuracy = double.IsNaN(metrics.MicroAccuracy) ? 0d : metrics.MicroAccuracy;
        var version = $"resume-category-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var storagePath = BuildModelStoragePath(version);
        var absolutePath = ResolveAbsolutePath(storagePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        mlContext.Model.Save(model, dataView.Schema, absolutePath);

        await DeactivateActiveVersionsAsync(ResumeCategoryModelType, cancellationToken);

        var modelVersion = new ModelVersion
        {
            Version = version,
            ModelType = ResumeCategoryModelType,
            StoragePath = storagePath,
            Accuracy = Math.Round(accuracy, 4),
            TrainingSampleCount = rows.Count,
            TrainingCategoryCount = categoryCount,
            TrainingCategoryDistribution = JsonSerializer.Serialize(categoryDistribution),
            IsActive = true,
            TrainedUtc = DateTime.UtcNow
        };

        _dbContext.ModelVersions.Add(modelVersion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Resume category model trained and saved to {StoragePath}. Accuracy={Accuracy:0.0000}",
            modelVersion.StoragePath,
            modelVersion.Accuracy);

        return MapToDto(modelVersion);
    }

    private async Task DeactivateActiveVersionsAsync(string modelType, CancellationToken cancellationToken)
    {
        var activeRows = await _dbContext.ModelVersions
            .Where(x => x.ModelType == modelType && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var row in activeRows)
        {
            row.IsActive = false;
        }
    }

    private string BuildModelStoragePath(string version)
    {
        var path = Path.Combine(_modelDirectory, $"{version}.zip");
        return Path.IsPathRooted(path) ? path : path.Replace('\\', '/');
    }

    private string ResolveAbsolutePath(string storagePath)
    {
        if (Path.IsPathRooted(storagePath))
        {
            return storagePath;
        }

        var normalized = storagePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(_contentRootPath, normalized));
    }

    private static ModelVersionDto MapToDto(ModelVersion row)
    {
        return new ModelVersionDto(
            row.Id,
            row.Version,
            row.ModelType,
            row.StoragePath,
            row.Accuracy,
            row.TrainingSampleCount,
            row.TrainingCategoryCount,
            row.TrainingCategoryDistribution,
            row.IsActive,
            row.TrainedUtc);
    }

    private sealed class ResumeCategoryTrainingInput
    {
        public string Text { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
