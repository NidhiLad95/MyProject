using GenxAi_Solutions.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace GenxAi_Solutions.Utils.Middleware
{
    /// <summary>
    /// Logs presence of Authorization: Bearer <token> for API requests,
    /// and writes a masked token into the Audit log.
    /// </summary>
    public class JwtHeaderLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAuditLogger _audit;

        public JwtHeaderLoggingMiddleware(RequestDelegate next, IAuditLogger audit)
        {
            _next = next;
            _audit = audit;
        }

        public async Task Invoke(HttpContext context)
        {
            // Only look at API routes; skip MVC pages/assets
            var path = context.Request.Path.HasValue ? context.Request.Path.Value! : string.Empty;
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                bool hasAuth = context.Request.Headers.TryGetValue("Authorization", out StringValues header);
                var auth = hasAuth ? header.ToString() : null;

                if (!string.IsNullOrWhiteSpace(auth) &&
                    auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = auth.Substring("Bearer ".Length).Trim();
                    var masked = Mask(token);

                    _audit.LogSecurityEvent(
                        eventType: "JWT_PRESENT",
                        username: context.User?.Identity?.Name ?? "anonymous",
                        ipAddress: GetClientIp(context),
                       // details: $"Bearer token found; masked={masked}"
                        details: $"Bearer token found; masked={token} , {path}"
                    );
                }
                else
                {
                    _audit.LogSecurityEvent(
                        eventType: "JWT_MISSING",
                        username: context.User?.Identity?.Name ?? "anonymous",
                        ipAddress: GetClientIp(context),
                        details: $"No Authorization: Bearer header on API request; {path}"
                    );
                }
            }

            await _next(context);
        }

        private static string Mask(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return "<empty>";
            if (token.Length <= 14) return new string('*', token.Length);
            // keep 8 leading + 6 trailing chars, mask the middle
            return token.Substring(0, 8) + "..." + token.Substring(token.Length - 6);
        }

        private static string GetClientIp(HttpContext ctx)
        {
            // Respect proxy header if present
            if (ctx.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd))
            {
                var first = fwd.ToString()?.Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(first)) return first!;
            }
            return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
