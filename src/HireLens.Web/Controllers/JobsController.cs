using System.Security.Claims;
using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireLens.Web.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize(Policy = "RecruiterOrAdmin")]
public sealed class JobsController(IJobPostingService jobPostingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobPostingDto>>> GetAll(CancellationToken cancellationToken)
    {
        var jobs = await jobPostingService.GetAllAsync(cancellationToken);
        return Ok(jobs);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobPostingDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobPostingService.GetByIdAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpPost]
    public async Task<ActionResult<JobPostingDto>> Create([FromBody] JobPostingUpsertRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var created = await jobPostingService.CreateAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<JobPostingDto>> Update(Guid id, [FromBody] JobPostingUpsertRequest request, CancellationToken cancellationToken)
    {
        var updated = await jobPostingService.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await jobPostingService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
