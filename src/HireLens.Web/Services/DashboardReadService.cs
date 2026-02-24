using HireLens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HireLens.Web.Services;

internal sealed class DashboardReadService(IDbContextFactory<HireLensDbContext> dbContextFactory) : IDashboardReadService
{
    private readonly IDbContextFactory<HireLensDbContext> _dbContextFactory = dbContextFactory;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var jobsCount = await dbContext.JobPostings.AsNoTracking().CountAsync(cancellationToken);
        var candidatesCount = await dbContext.Candidates.AsNoTracking().CountAsync(cancellationToken);
        var analysesCount = await dbContext.ResumeAnalyses.AsNoTracking().CountAsync(cancellationToken);
        var matchesCount = await dbContext.MatchResults.AsNoTracking().CountAsync(cancellationToken);

        var latestJobs = await dbContext.JobPostings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedUtc)
            .Take(5)
            .Select(x => new DashboardJobDto(
                x.Id,
                x.Title,
                x.SeniorityLevel.ToString(),
                x.UpdatedUtc))
            .ToListAsync(cancellationToken);

        var latestCandidates = await dbContext.Candidates
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .Take(5)
            .Select(x => new DashboardCandidateDto(
                x.Id,
                x.FullName,
                x.Email,
                x.CreatedUtc))
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto(
            jobsCount,
            candidatesCount,
            analysesCount,
            matchesCount,
            latestJobs,
            latestCandidates);
    }

    public async Task<IReadOnlyList<JobLookupDto>> GetJobLookupAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.JobPostings
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedUtc)
            .Select(x => new JobLookupDto(x.Id, x.Title))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchResultRowDto>> GetMatchResultsAsync(Guid? jobPostingId = null, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query =
            from match in dbContext.MatchResults.AsNoTracking()
            join job in dbContext.JobPostings.AsNoTracking()
                on match.JobPostingId equals job.Id
            join candidate in dbContext.Candidates.AsNoTracking()
                on match.CandidateId equals candidate.Id
            join analysis in dbContext.ResumeAnalyses.AsNoTracking()
                on match.ResumeAnalysisId equals analysis.Id into analysisGroup
            from analysis in analysisGroup.DefaultIfEmpty()
            select new
            {
                Match = match,
                Job = job,
                Candidate = candidate,
                Analysis = analysis
            };

        if (jobPostingId is not null)
        {
            query = query.Where(x => x.Match.JobPostingId == jobPostingId.Value);
        }

        return await query
            .OrderByDescending(x => x.Match.MatchScore)
            .ThenByDescending(x => x.Match.GeneratedUtc)
            .Select(x => new MatchResultRowDto(
                x.Match.Id,
                x.Job.Id,
                x.Job.Title,
                x.Candidate.Id,
                x.Candidate.FullName,
                x.Match.MatchScore,
                x.Match.MatchedSkills,
                x.Match.MissingSkills,
                x.Match.TopOverlappingKeywords,
                x.Analysis != null ? x.Analysis.PredictedCategory : "N/A",
                x.Analysis != null ? x.Analysis.ConfidenceScore : null,
                x.Match.GeneratedUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelVersionRowDto>> GetModelVersionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.ModelVersions
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
