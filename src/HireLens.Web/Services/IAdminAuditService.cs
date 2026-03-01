namespace HireLens.Web.Services;

public interface IAdminAuditService
{
    void Log(HttpContext httpContext, string action, string outcome, string? details = null);
}
