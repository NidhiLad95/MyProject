using GenxAi_Solutions_V1.Utils.Middleware;

namespace GenxAi_Solutions_V1.Utils
{
    /// <summary>
    /// Registers middleware in correct order.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseGenxAiCorePipeline(this IApplicationBuilder app)
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
            //app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseMiddleware<JwtHeaderLoggingMiddleware>(); // Jwt Header nLogging
            app.UseMiddleware<AuditLoggingMiddleware>(); // Add audit logging
            app.UseMiddleware<PerformanceMonitoringMiddleware>();
            app.UseMiddleware<ErrorHandlingMiddleware>();
            return app;
        }
    }
}
