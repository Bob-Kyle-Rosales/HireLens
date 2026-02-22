using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HireLens.Application.DTOs;
using HireLens.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HireLens.Web.Controllers;

[ApiController]
[Route("api/candidates")]
[Authorize(Policy = "RecruiterOrAdmin")]
public sealed class CandidatesController(ICandidateService candidateService) : ControllerBase
{
    private const long MaxResumeSizeBytes = 10 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CandidateDto>>> GetAll(CancellationToken cancellationToken)
    {
        var candidates = await candidateService.GetAllAsync(cancellationToken);
        return Ok(candidates);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidateDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var candidate = await candidateService.GetByIdAsync(id, cancellationToken);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxResumeSizeBytes)]
    public async Task<ActionResult<CandidateDto>> Upload([FromForm] CandidateUploadForm form, CancellationToken cancellationToken)
    {
        if (form.Resume is null || form.Resume.Length == 0)
        {
            return BadRequest("Resume file is required.");
        }

        if (form.Resume.Length > MaxResumeSizeBytes)
        {
            return BadRequest("Resume file exceeds 10 MB limit.");
        }

        await using var stream = form.Resume.OpenReadStream();
        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";

        var request = new CandidateUploadRequest
        {
            FullName = form.FullName.Trim(),
            Email = form.Email.Trim(),
            ResumeFileName = form.Resume.FileName,
            ResumeContentType = string.IsNullOrWhiteSpace(form.Resume.ContentType) ? "application/octet-stream" : form.Resume.ContentType,
            ResumeContent = memory.ToArray(),
            UploadedByUserId = userId
        };

        var created = await candidateService.UploadAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}

public sealed class CandidateUploadForm
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public IFormFile Resume { get; set; } = default!;
}
