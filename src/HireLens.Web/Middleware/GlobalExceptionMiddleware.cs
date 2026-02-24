using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace HireLens.Web.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing request {Path}", context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            var statusCode = ex is InvalidOperationException
                ? HttpStatusCode.BadRequest
                : HttpStatusCode.InternalServerError;
            context.Response.StatusCode = (int)statusCode;

            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = "application/json";
                var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
                    ? value?.ToString()
                    : context.TraceIdentifier;

                var details = new ProblemDetails
                {
                    Type = "https://httpstatuses.com/" + context.Response.StatusCode,
                    Title = statusCode == HttpStatusCode.BadRequest ? "Request validation failed" : "Unhandled exception",
                    Detail = statusCode == HttpStatusCode.BadRequest
                        ? ex.Message
                        : "An unexpected error occurred.",
                    Status = context.Response.StatusCode,
                    Instance = context.Request.Path
                };
                details.Extensions["correlationId"] = correlationId;

                await context.Response.WriteAsJsonAsync(details);
                return;
            }

            context.Response.Redirect("/Error");
        }
    }
}
