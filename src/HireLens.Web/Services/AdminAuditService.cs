using System.Diagnostics;
using System.Security.Claims;

namespace HireLens.Web.Services;

internal sealed class AdminAuditService(ILogger<AdminAuditService> logger) : IAdminAuditService
{
    private readonly ILogger<AdminAuditService> _logger = logger;

    public void Log(HttpContext httpContext, string action, string outcome, string? details = null)
    {
        var user = httpContext.User;
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var userName = user.Identity?.Name ?? "unknown";
        var roles = string.Join(
            ',',
            user.FindAll(ClaimTypes.Role)
                .Select(x => x.Value)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogInformation(
            "AdminAudit action={Action} outcome={Outcome} userId={UserId} userName={UserName} roles={Roles} ip={RemoteIp} method={Method} path={Path} traceId={TraceId} details={Details}",
            action,
            outcome,
            userId,
            userName,
            string.IsNullOrWhiteSpace(roles) ? "none" : roles,
            remoteIp,
            httpContext.Request.Method,
            httpContext.Request.Path.ToString(),
            traceId,
            details ?? string.Empty);
    }
}
