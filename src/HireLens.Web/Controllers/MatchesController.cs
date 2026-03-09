using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireLens.Web.Controllers;

[ApiController]
[Route("api/matches")]
[Authorize(Policy = "RecruiterOrAdmin")]
public sealed class MatchesController(IMatchingService matchingService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchResultDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var row = await matchingService.GetByIdAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("job/{jobPostingId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MatchResultDto>>> GetByJobPostingId(
        Guid jobPostingId,
        CancellationToken cancellationToken)
    {
        var rows = await matchingService.GetByJobPostingIdAsync(jobPostingId, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("job/{jobPostingId:guid}/paged")]
    public async Task<ActionResult<PagedResult<MatchResultDto>>> GetByJobPostingIdPaged(
        Guid jobPostingId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var page = await matchingService.GetByJobPostingIdPagedAsync(jobPostingId, pageNumber, pageSize, cancellationToken);
        return Ok(page);
    }

    [HttpPost("run")]
    public async Task<ActionResult<IReadOnlyList<MatchResultDto>>> RunForJob(
        [FromBody] MatchCandidatesRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await matchingService.MatchForJobAsync(request, cancellationToken);
        return Ok(rows);
    }
}
