using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Web.Services;

internal sealed class DashboardReadService(HireLensDbContext dbContext) : IDashboardReadService
{
    private readonly HireLensDbContext _dbContext = dbContext;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var jobsTask = _dbContext.JobPostings.AsNoTracking().CountAsync(cancellationToken);
        var candidatesTask = _dbContext.Candidates.AsNoTracking().CountAsync(cancellationToken);
        var analysesTask = _dbContext.ResumeAnalyses.AsNoTracking().CountAsync(cancellationToken);
        var matchesTask = _dbContext.MatchResults.AsNoTracking().CountAsync(cancellationToken);

        var latestJobsTask = _dbContext.JobPostings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedUtc)
            .Take(5)
            .Select(x => new DashboardJobDto(
                x.Id,
                x.Title,
                x.SeniorityLevel.ToString(),
                x.UpdatedUtc))
            .ToListAsync(cancellationToken);

        var latestCandidatesTask = _dbContext.Candidates
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .Take(5)
            .Select(x => new DashboardCandidateDto(
                x.Id,
                x.FullName,
                x.Email,
                x.CreatedUtc))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(jobsTask, candidatesTask, analysesTask, matchesTask, latestJobsTask, latestCandidatesTask);

        return new DashboardSummaryDto(
            jobsTask.Result,
            candidatesTask.Result,
            analysesTask.Result,
            matchesTask.Result,
            latestJobsTask.Result,
            latestCandidatesTask.Result);
    }

    public async Task<IReadOnlyList<JobLookupDto>> GetJobLookupAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.JobPostings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedUtc)
            .Select(x => new JobLookupDto(x.Id, x.Title))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchResultRowDto>> GetMatchResultsAsync(Guid? jobPostingId = null, CancellationToken cancellationToken = default)
    {
        var query =
            from match in _dbContext.MatchResults.AsNoTracking()
            join job in _dbContext.JobPostings.AsNoTracking() on match.JobPostingId equals job.Id
            join candidate in _dbContext.Candidates.AsNoTracking() on match.CandidateId equals candidate.Id
            join analysis in _dbContext.ResumeAnalyses.AsNoTracking()
                on match.ResumeAnalysisId equals analysis.Id into analysisGroup
            from analysis in analysisGroup.DefaultIfEmpty()
            select new MatchResultRowDto(
                match.Id,
                job.Id,
                job.Title,
                candidate.Id,
                candidate.FullName,
                match.MatchScore,
                match.MatchedSkills,
                match.MissingSkills,
                match.TopOverlappingKeywords,
                analysis != null ? analysis.PredictedCategory : "N/A",
                analysis != null ? analysis.ConfidenceScore : null,
                match.GeneratedUtc);

        if (jobPostingId is not null)
        {
            query = query.Where(x => x.JobPostingId == jobPostingId.Value);
        }

        return await query
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.GeneratedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelVersionRowDto>> GetModelVersionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ModelVersions
            .AsNoTracking()
            .OrderByDescending(x => x.TrainedUtc)
            .Select(x => new ModelVersionRowDto(
                x.Id,
                x.Version,
                x.ModelType,
                x.Accuracy,
                x.IsActive,
                x.TrainedUtc,
                x.StoragePath))
            .ToListAsync(cancellationToken);
    }
}
