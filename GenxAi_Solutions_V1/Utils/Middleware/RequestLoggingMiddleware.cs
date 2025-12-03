using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GenxAi_Solutions_V1.Utils.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private const int MaxLoggedBodyBytes = 8 * 1024; // 8 KB

        private static readonly HashSet<string> _sensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "Set-Cookie", "X-Api-Key"
        };

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var req = context.Request;
            var correlationId = context.TraceIdentifier;
            string requestBodyPreview = string.Empty;

            // Capture small body if JSON/form
            if (req.ContentLength.HasValue && req.ContentLength.Value <= MaxLoggedBodyBytes &&
                (req.ContentType?.Contains("application/json") == true ||
                 req.ContentType?.Contains("application/x-www-form-urlencoded") == true))
            {
                req.EnableBuffering();
                using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
                requestBodyPreview = await reader.ReadToEndAsync();
                if (requestBodyPreview.Length > MaxLoggedBodyBytes)
                    requestBodyPreview = requestBodyPreview.Substring(0, MaxLoggedBodyBytes);
                req.Body.Position = 0;
            }

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var clientIp = context.Connection.RemoteIpAddress?.ToString();
                var status = context.Response?.StatusCode;

                var headers = req.Headers
                    .Where(h => !_sensitiveHeaders.Contains(h.Key))
                    .ToDictionary(k => k.Key, v => string.Join(",", v.Value));

                _logger.LogInformation(
                    "HTTP {Method} {Path}{Query} -> {Status} in {Elapsed} ms | CorrelationId={CorrelationId} | Ip={ClientIp} | User={User} | Headers={Headers} | BodyPreview={Body}",
                    req.Method,
                    req.Path,
                    req.QueryString.HasValue ? req.QueryString.Value : "",
                    status,
                    sw.ElapsedMilliseconds,
                    correlationId,
                    clientIp,
                    context.User?.Identity?.Name ?? "anonymous",
                    headers,
                    requestBodyPreview);
            }
        }
    }
}
