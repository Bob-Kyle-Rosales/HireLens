using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HireLens.Web.Controllers;

[ApiController]
[Route("api/models")]
[Authorize(Policy = "RecruiterOrAdmin")]
[EnableRateLimiting("admin-heavy")]
public sealed class ModelsController(
    IModelVersionService modelVersionService,
    IResumeAnalysisService resumeAnalysisService) : ControllerBase
{
    private const string ResumeCategoryModelType = "ResumeCategoryClassifier";

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModelVersionDto>>> GetAll(CancellationToken cancellationToken)
    {
        var rows = await modelVersionService.GetAllAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ModelVersionDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var row = await modelVersionService.GetByIdAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("active")]
    public async Task<ActionResult<ModelVersionDto>> GetActive(
        [FromQuery] string modelType = ResumeCategoryModelType,
        CancellationToken cancellationToken = default)
    {
        var row = await modelVersionService.GetActiveAsync(modelType, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("train/resume-category")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ModelVersionDto>> TrainResumeCategoryModel(CancellationToken cancellationToken)
    {
        var modelVersion = await modelVersionService.TrainResumeCategoryModelAsync(cancellationToken);
        return Ok(modelVersion);
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetActive(Guid id, CancellationToken cancellationToken)
    {
        var activated = await modelVersionService.SetActiveAsync(id, cancellationToken);
        return activated ? NoContent() : NotFound();
    }

    [HttpPost("reanalyze-candidates")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ReanalyzeCandidatesResponse>> ReanalyzeCandidates(CancellationToken cancellationToken)
    {
        var analyzedCount = await resumeAnalysisService.AnalyzeAllCandidatesAsync(cancellationToken);
        return Ok(new ReanalyzeCandidatesResponse(analyzedCount));
    }
}

public sealed record ReanalyzeCandidatesResponse(int AnalyzedCount);
