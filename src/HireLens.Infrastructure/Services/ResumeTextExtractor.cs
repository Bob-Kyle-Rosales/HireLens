using System.Text;
using UglyToad.PdfPig;

namespace HireLens.Infrastructure.Services;

internal sealed class ResumeTextExtractor : IResumeTextExtractor
{
    public async Task<string> ExtractAsync(
        byte[] fileContent,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (fileContent.Length == 0)
        {
            return string.Empty;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".txt" || contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            await using var memoryStream = new MemoryStream(fileContent);
            using var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        if (extension == ".pdf" || contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            await using var memoryStream = new MemoryStream(fileContent);
            using var pdf = PdfDocument.Open(memoryStream);
            var builder = new StringBuilder();

            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.AppendLine(page.Text);
            }

            return builder.ToString();
        }

        throw new InvalidOperationException("Unsupported file format. Please upload a PDF or text file.");
    }
}
