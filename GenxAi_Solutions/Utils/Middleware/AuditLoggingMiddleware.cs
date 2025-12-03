using GenxAi_Solutions.Services.Interfaces;
using Microsoft.Extensions.Primitives;

namespace GenxAi_Solutions.Utils.Middleware
{
    public class AuditLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAuditLogger _auditLogger;

        public AuditLoggingMiddleware(RequestDelegate next, IAuditLogger auditLogger)
        {
            _next = next;
            _auditLogger = auditLogger;
        }

        public async Task Invoke(HttpContext context)
        {
            // Skip audit for static files
            if (IsStaticFile(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var username = context.User?.Identity?.Name ?? "anonymous";
            var ipAddress = GetClientIpAddress(context);
            var method = context.Request.Method;
            var path = context.Request.Path;

            // Log sensitive actions
            if (IsSensitiveAction(path, method))
            {
                _auditLogger.LogGeneralAudit($"{method} {path}", username, ipAddress, "Sensitive action accessed");
            }

            await _next(context);

            // Log specific status codes
            if (context.Response.StatusCode == 401 || context.Response.StatusCode == 403)
            {
                _auditLogger.LogSecurityEvent("UNAUTHORIZED_ACCESS", username, ipAddress,
                    $"Attempted to access {method} {path}, Status: {context.Response.StatusCode}");
            }
        }

        private bool IsStaticFile(PathString path)
        {
            var staticFileExtensions = new[] { ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".woff", ".woff2" };
            return staticFileExtensions.Any(ext => path.Value.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsSensitiveAction(PathString path, string method)
        {
            var sensitivePaths = new[]
            {
                "/User/Login",
                "/User/Logout",
                "/User/Register",
                "/Admin/",
                "/api/",
                "/Account/"
            };

            return sensitivePaths.Any(p => path.Value.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Check for forwarded header first (behind proxy)
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues forwardedIp))
            {
                return forwardedIp.FirstOrDefault()?.Split(',').First().Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
