using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using HireLens.Web.Services;
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
    IResumeAnalysisService resumeAnalysisService,
    IAdminAuditService adminAuditService) : ControllerBase
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
        try
        {
            var modelVersion = await modelVersionService.TrainResumeCategoryModelAsync(cancellationToken);
            adminAuditService.Log(
                HttpContext,
                action: "Model.TrainResumeCategory",
                outcome: "Success",
                details: $"modelVersionId={modelVersion.Id};version={modelVersion.Version};accuracy={modelVersion.Accuracy:0.0000}");

            return Ok(modelVersion);
        }
        catch (Exception ex)
        {
            adminAuditService.Log(HttpContext, "Model.TrainResumeCategory", "Failed", ex.Message);
            throw;
        }
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetActive(Guid id, CancellationToken cancellationToken)
    {
        var activated = await modelVersionService.SetActiveAsync(id, cancellationToken);

        if (!activated)
        {
            adminAuditService.Log(
                HttpContext,
                action: "Model.ActivateVersion",
                outcome: "NotFound",
                details: $"modelVersionId={id}");

            return NotFound();
        }

        adminAuditService.Log(
            HttpContext,
            action: "Model.ActivateVersion",
            outcome: "Success",
            details: $"modelVersionId={id}");

        return NoContent();
    }

    [HttpPost("reanalyze-candidates")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ReanalyzeCandidatesResponse>> ReanalyzeCandidates(CancellationToken cancellationToken)
    {
        try
        {
            var analyzedCount = await resumeAnalysisService.AnalyzeAllCandidatesAsync(cancellationToken);
            adminAuditService.Log(
                HttpContext,
                action: "Model.ReanalyzeCandidates",
                outcome: "Success",
                details: $"analyzedCount={analyzedCount}");

            return Ok(new ReanalyzeCandidatesResponse(analyzedCount));
        }
        catch (Exception ex)
        {
            adminAuditService.Log(HttpContext, "Model.ReanalyzeCandidates", "Failed", ex.Message);
            throw;
        }
    }
}

public sealed record ReanalyzeCandidatesResponse(int AnalyzedCount);
