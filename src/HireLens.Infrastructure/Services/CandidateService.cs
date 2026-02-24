using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using HireLens.Domain.Entities;
using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Services;

internal sealed class CandidateService(
    HireLensDbContext dbContext,
    IResumeTextExtractor resumeTextExtractor,
    IResumeAnalysisService resumeAnalysisService,
    ILogger<CandidateService> logger) : ICandidateService
{
    private readonly HireLensDbContext _dbContext = dbContext;
    private readonly IResumeTextExtractor _resumeTextExtractor = resumeTextExtractor;
    private readonly IResumeAnalysisService _resumeAnalysisService = resumeAnalysisService;
    private readonly ILogger<CandidateService> _logger = logger;

    public async Task<IReadOnlyList<CandidateDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _dbContext.Candidates
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        return candidates.Select(MapToDto).ToList();
    }

    public async Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var candidate = await _dbContext.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return candidate is null ? null : MapToDto(candidate);
    }

    public async Task<CandidateDto> UploadAsync(CandidateUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ResumeContent.Length == 0)
        {
            throw new InvalidOperationException("Resume file cannot be empty.");
        }

        var resumeText = await _resumeTextExtractor.ExtractAsync(
            request.ResumeContent,
            request.ResumeFileName,
            request.ResumeContentType,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(resumeText))
        {
            throw new InvalidOperationException("No readable text could be extracted from the uploaded resume.");
        }

        var candidate = new Candidate
        {
            UploadedByUserId = request.UploadedByUserId,
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            ResumeFileName = request.ResumeFileName,
            ResumeContentType = request.ResumeContentType,
            ResumeText = resumeText,
            CreatedUtc = DateTime.UtcNow
        };

        _dbContext.Candidates.Add(candidate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _resumeAnalysisService.AnalyzeCandidateAsync(candidate.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Candidate {CandidateId} uploaded but automatic resume analysis failed", candidate.Id);
        }

        _logger.LogInformation("Candidate {CandidateId} uploaded", candidate.Id);
        return MapToDto(candidate);
    }

    private static CandidateDto MapToDto(Candidate candidate)
    {
        return new CandidateDto(
            candidate.Id,
            candidate.FullName,
            candidate.Email,
            candidate.ResumeFileName,
            candidate.CreatedUtc);
    }
}
