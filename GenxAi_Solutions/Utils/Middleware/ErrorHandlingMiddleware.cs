//using System.Net;
//using System.Text.Json;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;

//namespace GenxAi_Solutions.Utils.Middleware
//{
//    public class ErrorHandlingMiddleware
//    {
//        private readonly RequestDelegate _next;
//        private readonly ILogger<ErrorHandlingMiddleware> _logger;
//        private readonly IHostEnvironment _env;

//        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment env)
//        {
//            _next = next;
//            _logger = logger;
//            _env = env;
//        }

//        //public async Task Invoke(HttpContext context)
//        //{
//        //    try
//        //    {
//        //        await _next(context);
//        //    }
//        //    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
//        //    {
//        //        _logger.LogInformation("Request aborted by client: {Method} {Path}", context.Request.Method, context.Request.Path);
//        //        context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        var correlationId = context.TraceIdentifier;

//        //        var problem = new ProblemDetails
//        //        {
//        //            Type = "https://httpstatuses.com/500",
//        //            Title = "An unexpected error occurred.",
//        //            Status = (int)HttpStatusCode.InternalServerError,
//        //            Detail = _env.IsDevelopment() ? ex.ToString() : "Please contact support with the provided correlationId.",
//        //            Instance = $"{context.Request.Method} {context.Request.Path}"
//        //        };
//        //        problem.Extensions["correlationId"] = correlationId;

//        //        _logger.LogError(ex, "Unhandled exception {Method} {Path} {CorrelationId}",
//        //            context.Request.Method, context.Request.Path, correlationId);

//        //        context.Response.ContentType = "application/problem+json";
//        //        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
//        //        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
//        //        {
//        //            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//        //        });
//        //        await context.Response.WriteAsync(json);
//        //    }
//        //}


//        public async Task Invoke(HttpContext context)
//        {
//            try
//            {
//                await _next(context);
//            }
//            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
//            {
//                _logger.LogInformation("Request aborted by client: {Method} {Path}", context.Request.Method, context.Request.Path);
//                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
//            }
//            catch (Exception ex)
//            {
//                var correlationId = context.TraceIdentifier;

//                // Enhanced error logging with stack trace
//                _logger.LogError(ex,
//                    "Unhandled exception {Method} {Path} {CorrelationId}. " +
//                    "Exception Details: {ExceptionType} - {ExceptionMessage} " +
//                    "Stack Trace: {StackTrace}",
//                    context.Request.Method,
//                    context.Request.Path,
//                    correlationId,
//                    ex.GetType().FullName,
//                    ex.Message,
//                    ex.StackTrace);

//                var problem = new ProblemDetails
//                {
//                    Type = "https://httpstatuses.com/500",
//                    Title = "An unexpected error occurred.",
//                    Status = (int)HttpStatusCode.InternalServerError,
//                    Detail = _env.IsDevelopment() ? ex.ToString() : "Please contact support with the provided correlationId.",
//                    Instance = $"{context.Request.Method} {context.Request.Path}"
//                };
//                problem.Extensions["correlationId"] = correlationId;

//                context.Response.ContentType = "application/problem+json";
//                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
//                var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
//                {
//                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//                });
//                await context.Response.WriteAsync(json);
//            }
//        }
//    }
//}


using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace GenxAi_Solutions.Utils.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                _logger.LogInformation("Request aborted by client: {Method} {Path}", context.Request.Method, context.Request.Path);

                var userFriendlyResponse = new
                {
                    error = "Request cancelled",
                    message = "Your request was cancelled. Please try again.",
                    correlationId = context.TraceIdentifier
                };

                await WriteUserFriendlyResponse(context, StatusCodes.Status499ClientClosedRequest, userFriendlyResponse);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var correlationId = context.TraceIdentifier;
            var userMessage = GetUserFriendlyMessage(exception);
            var developerMessage = _env.IsDevelopment() ? exception.ToString() : null;

            // Enhanced error logging
            _logger.LogError(exception,
                "Unhandled exception {Method} {Path} {CorrelationId}. " +
                "Exception Details: {ExceptionType} - {ExceptionMessage} " +
                "Stack Trace: {StackTrace}",
                context.Request.Method,
                context.Request.Path,
                correlationId,
                exception.GetType().FullName,
                exception.Message,
                exception.StackTrace);

            var statusCode = GetStatusCode(exception);
            var errorType = GetErrorType(exception);

            var userFriendlyResponse = new
            {
                error = errorType,
                message = userMessage,
                correlationId = correlationId,
                details = developerMessage,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            await WriteUserFriendlyResponse(context, statusCode, userFriendlyResponse);
        }

        private async Task WriteUserFriendlyResponse(HttpContext context, int statusCode, object response)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _env.IsDevelopment()
            };

            var json = JsonSerializer.Serialize(response, jsonOptions);
            await context.Response.WriteAsync(json);
        }

        private string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                UserFriendlyException userEx => userEx.Message, // Use the user-friendly message directly
                //ValidationException valEx => "Please correct the validation errors and try again.",
                //BusinessRuleException bizEx => bizEx.Message,
                UnauthorizedAccessException => "You don't have permission to access this resource.",
                TimeoutException => "The request timed out. Please try again.",
                ArgumentException => "There was a problem with your request. Please check your input and try again.",
                InvalidOperationException => "The operation could not be completed. Please try again.",
                SqlException sqlEx => HandleSqlException(sqlEx),
                HttpRequestException => "Unable to connect to external service. Please try again later.",
                _ => "An unexpected error occurred. Our team has been notified and is working on it."
            };
        }

        private string HandleSqlException(SqlException sqlEx)
        {
            // Handle common SQL Server errors
            return sqlEx.Number switch
            {
                547 => "This operation cannot be completed because the record is referenced by other data.",
                2601 => "A record with this information already exists.",
                2627 => "A record with this information already exists.",
                18456 => "Database connection failed. Please try again.",
                4060 => "Unable to connect to the database. Please try again later.",
                208 => "Requested resource was not found.",
                _ => "A database error occurred. Please try again or contact support if the problem persists."
            };
        }

        private int GetStatusCode(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                ArgumentException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                TimeoutException => StatusCodes.Status408RequestTimeout,
                NotImplementedException => StatusCodes.Status501NotImplemented,
                SqlException => StatusCodes.Status503ServiceUnavailable, // or 500 based on error number
                _ => StatusCodes.Status500InternalServerError
            };
        }

        private string GetErrorType(Exception exception)
        {
            return exception switch
            {
                UnauthorizedAccessException => "forbidden",
                ArgumentException => "bad_request",
                KeyNotFoundException => "not_found",
                TimeoutException => "timeout",
                NotImplementedException => "not_implemented",
                SqlException => "database_error",
                HttpRequestException => "service_unavailable",
                _ => "internal_server_error"
            };
        }

        //private string GetUserFriendlyMessage(Exception exception)
        //{
        //    return exception switch
        //    {
        //        UserFriendlyException userEx => userEx.Message, // Use the user-friendly message directly
        //        ValidationException valEx => "Please correct the validation errors and try again.",
        //        BusinessRuleException bizEx => bizEx.Message,
        //        UnauthorizedAccessException => "You don't have permission to access this resource.",
        //        TimeoutException => "The request timed out. Please try again.",
        //        ArgumentException => "There was a problem with your request. Please check your input and try again.",
        //        InvalidOperationException => "The operation could not be completed. Please try again.",
        //        SqlException sqlEx => HandleSqlException(sqlEx),
        //        HttpRequestException => "Unable to connect to external service. Please try again later.",
        //        _ => "An unexpected error occurred. Our team has been notified and is working on it."
        //    };
        //}
    }
}
