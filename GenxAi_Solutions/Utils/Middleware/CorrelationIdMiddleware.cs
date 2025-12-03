using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace GenxAi_Solutions.Utils.Middleware
{
    public class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-ID";
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            // Read incoming header or generate a new one
            if (context.Request.Headers.TryGetValue(HeaderName, out StringValues supplied)
                && Guid.TryParse(supplied.FirstOrDefault(), out var parsedGuid))
            {
                context.TraceIdentifier = parsedGuid.ToString();
            }
            else
            {
                context.TraceIdentifier = Guid.NewGuid().ToString();
            }

            // Add the header to response
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey(HeaderName))
                    context.Response.Headers.Add(HeaderName, context.TraceIdentifier);
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
