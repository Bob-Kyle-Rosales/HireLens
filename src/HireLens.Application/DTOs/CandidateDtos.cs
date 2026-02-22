using System.ComponentModel.DataAnnotations;

namespace HireLens.Application.DTOs;

public sealed record CandidateDto(
    Guid Id,
    string FullName,
    string Email,
    string ResumeFileName,
    DateTime CreatedUtc);

public sealed class CandidateUploadRequest
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string ResumeFileName { get; set; } = string.Empty;

    [Required]
    public string ResumeContentType { get; set; } = string.Empty;

    [Required]
    public byte[] ResumeContent { get; set; } = [];

    public string UploadedByUserId { get; set; } = string.Empty;
}
