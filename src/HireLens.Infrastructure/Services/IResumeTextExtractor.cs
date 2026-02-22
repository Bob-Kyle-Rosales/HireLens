namespace HireLens.Infrastructure.Services;

internal interface IResumeTextExtractor
{
    Task<string> ExtractAsync(
        byte[] fileContent,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
