using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireLens.Web.Controllers;

[ApiController]
[Route("api/analyses")]
[Authorize(Policy = "RecruiterOrAdmin")]
public sealed class ResumeAnalysesController(IResumeAnalysisService resumeAnalysisService) : ControllerBase
{
    [HttpGet("candidate/{candidateId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ResumeAnalysisDto>>> GetByCandidateId(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var rows = await resumeAnalysisService.GetByCandidateIdAsync(candidateId, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("candidate/{candidateId:guid}/latest")]
    public async Task<ActionResult<ResumeAnalysisDto>> GetLatestByCandidateId(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var row = await resumeAnalysisService.GetLatestByCandidateIdAsync(candidateId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("candidate/{candidateId:guid}/run")]
    public async Task<ActionResult<ResumeAnalysisDto>> AnalyzeCandidate(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var analysis = await resumeAnalysisService.AnalyzeCandidateAsync(candidateId, cancellationToken);
        return Ok(analysis);
    }
}
