using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HireLens.Web.Controllers;

[ApiController]
[Route("api/health")]
[Authorize(Policy = "AdminOnly")]
[EnableRateLimiting("admin-heavy")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "ok" });
    }
}
