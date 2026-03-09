using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using HireLens.Domain.Entities;
using HireLens.Infrastructure.Helpers;
using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Services;

internal sealed class JobPostingService(
    HireLensDbContext dbContext,
    ILogger<JobPostingService> logger) : IJobPostingService
{
    private readonly HireLensDbContext _dbContext = dbContext;
    private readonly ILogger<JobPostingService> _logger = logger;

    public async Task<IReadOnlyList<JobPostingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.JobPostings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedUtc)
            .ToListAsync(cancellationToken);

        return jobs.Select(MapToDto).ToList();
    }

    public async Task<PagedResult<JobPostingDto>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var (validatedPageNumber, validatedPageSize) = ValidatePaging(pageNumber, pageSize);

        var query = _dbContext.JobPostings
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(x =>
                x.Title.Contains(normalizedSearch) ||
                x.Description.Contains(normalizedSearch) ||
                x.RequiredSkills.Contains(normalizedSearch) ||
                x.OptionalSkills.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.UpdatedUtc)
            .Skip((validatedPageNumber - 1) * validatedPageSize)
            .Take(validatedPageSize)
            .Select(x => MapToDto(x))
            .ToListAsync(cancellationToken);

        return new PagedResult<JobPostingDto>(items, validatedPageNumber, validatedPageSize, totalCount);
    }

    public async Task<JobPostingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return job is null ? null : MapToDto(job);
    }

    public async Task<JobPostingDto> CreateAsync(JobPostingUpsertRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var entity = new JobPosting
        {
            CreatedByUserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            RequiredSkills = TextProcessing.JoinSkillList(request.RequiredSkills),
            OptionalSkills = TextProcessing.JoinSkillList(request.OptionalSkills),
            SeniorityLevel = request.SeniorityLevel,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _dbContext.JobPostings.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job posting created with id {JobId}", entity.Id);
        return MapToDto(entity);
    }

    public async Task<JobPostingDto?> UpdateAsync(Guid id, JobPostingUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.JobPostings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.RequiredSkills = TextProcessing.JoinSkillList(request.RequiredSkills);
        entity.OptionalSkills = TextProcessing.JoinSkillList(request.OptionalSkills);
        entity.SeniorityLevel = request.SeniorityLevel;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job posting {JobId} updated", entity.Id);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.JobPostings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbContext.JobPostings.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job posting {JobId} deleted", entity.Id);
        return true;
    }

    private static JobPostingDto MapToDto(JobPosting entity)
    {
        return new JobPostingDto(
            entity.Id,
            entity.Title,
            entity.Description,
            TextProcessing.ParseSkillList(entity.RequiredSkills),
            TextProcessing.ParseSkillList(entity.OptionalSkills),
            entity.SeniorityLevel,
            entity.CreatedUtc,
            entity.UpdatedUtc);
    }

    private static (int PageNumber, int PageSize) ValidatePaging(int pageNumber, int pageSize)
    {
        var validatedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var validatedPageSize = pageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => pageSize
        };

        return (validatedPageNumber, validatedPageSize);
    }
}
