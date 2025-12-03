using GenxAi_Solutions_V1.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.IdentityModel.JsonWebTokens; // or System.IdentityModel.Tokens.Jwt
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig.Logging;

namespace GenxAi_Solutions_V1.Services
{
    public class AuthEventsService
    {
        private readonly ILogger<AuthEventsService> _logger;

        public AuthEventsService(ILogger<AuthEventsService> logger)
        {
            _logger = logger;
        }

        public async Task OnSignedIn(CookieSignedInContext context)
        {
            try
            {
                var auditLogger = context.HttpContext.RequestServices.GetRequiredService<IAuditLogger>();
                var username = context.Principal?.Identity?.Name ?? "unknown";
                var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                auditLogger.LogUserLogin(username, ipAddress, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnSignedIn event");
                // Don't throw, as this shouldn't break the sign-in process
            }

            await Task.CompletedTask;
        }

        public async Task OnSigningOut(CookieSigningOutContext context)
        {
            try
            {
                var auditLogger = context.HttpContext.RequestServices.GetRequiredService<IAuditLogger>();
                var username = context.HttpContext.User?.Identity?.Name ?? "unknown";
                var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                auditLogger.LogUserLogout(username, ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnSigningOut event");
                // Don't throw, as this shouldn't break the sign-out process
            }

            await Task.CompletedTask;
        }

        public Task OnMessageReceived(MessageReceivedContext ctx)
        {
            // Prefer NOT logging raw JWTs; log a hash + a few safe claims if available.
            var token = ctx.Token
                ?? ctx.Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(token))
            {
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
                _logger.LogInformation("JWT received on {Path}. TokenHash={Hash}", ctx.HttpContext.Request.Path, hash);
            }
            else
            {
                _logger.LogInformation("No JWT present on {Path}", ctx.HttpContext.Request.Path);
            }

            return Task.CompletedTask;
        }

        public Task OnTokenValidated(TokenValidatedContext ctx)
        {
            var principal = ctx.Principal!;
            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value
                      ?? principal.FindFirst("jti")?.Value;
            var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value;

            var jwt = ctx.SecurityToken as JwtSecurityToken;
            _logger.LogInformation("JWT validated. sub={Sub} jti={Jti} exp={Exp:o}",
                sub, jti, jwt?.ValidTo);

            // OPTIONAL: token revocation check
            // if (jti != null && await _revocationStore.IsRevokedAsync(jti)) ctx.Fail("Token revoked");

            return Task.CompletedTask;
        }

        public Task OnAuthenticationFailed(AuthenticationFailedContext ctx)
        {
            _logger.LogError(ctx.Exception, "JWT authentication failed on {Path}", ctx.HttpContext.Request.Path);
            return Task.CompletedTask;
        }

        public Task OnChallenge(JwtBearerChallengeContext ctx)
        {
            // Replace default challenge (which adds WWW-Authenticate header) with JSON
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            ctx.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new
            {
                message = "Not authenticated",
                error = ctx.Error,
                detail = ctx.ErrorDescription
            });
            return ctx.Response.WriteAsync(payload);
        }

        public Task OnForbidden(ForbiddenContext ctx)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Forbidden" }));
        }

        // Manual "sign-out" hook you can call from your Logout endpoint
        public Task OnSigningOut(HttpContext http, ClaimsPrincipal? user)
        {
            var sub = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
            _logger.LogInformation("User requested sign-out. sub={Sub} path={Path}", sub, http.Request.Path);
            return Task.CompletedTask;
        }
    }
}
