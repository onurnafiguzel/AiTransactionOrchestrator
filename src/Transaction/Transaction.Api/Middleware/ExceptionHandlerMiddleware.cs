using BuildingBlocks.Contracts.Observability;
using System.Text.Json;
using Transaction.Domain.Common;

namespace Transaction.Api.Middleware;

/// <summary>
/// Global exception handler middleware
/// Catches all unhandled exceptions and returns structured error response
/// </summary>
public sealed class ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, logger);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, ILogger<ExceptionHandlerMiddleware> logger)
    {
        var correlationId = CorrelationContext.CorrelationId ?? 
            (context.Request.Headers.TryGetValue(Correlation.HeaderName, out var values) 
                ? values.ToString() 
                : Guid.NewGuid().ToString("N"));

        var response = context.Response;
        response.ContentType = "application/json";

        var (statusCode, title, detail) = MapExceptionToResponse(exception);
        
        response.StatusCode = statusCode;

        // Log exception with context
        LogException(exception, statusCode, correlationId, logger, context);

        var errorResponse = new
        {
            error = new
            {
                title = title,
                detail = detail,
                statusCode = statusCode,
                correlationId = correlationId,
                timestamp = DateTime.UtcNow,
                path = context.Request.Path.Value,
                method = context.Request.Method
            }
        };

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return response.WriteAsync(json);
    }

    private static (int statusCode, string title, string detail) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            // Domain exceptions - validation errors
            DomainException de => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                de.Message
            ),

            // Argument validation
            ArgumentNullException => (
                StatusCodes.Status400BadRequest,
                "Invalid Argument",
                "One or more required arguments were null"
            ),

            ArgumentException ae => (
                StatusCodes.Status400BadRequest,
                "Invalid Argument",
                ae.Message
            ),

            // Not found
            InvalidOperationException ioe when ioe.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) => (
                StatusCodes.Status404NotFound,
                "Not Found",
                ioe.Message
            ),

            // Timeout
            OperationCanceledException => (
                StatusCodes.Status408RequestTimeout,
                "Request Timeout",
                "The request took too long to complete. Please try again."
            ),

            TimeoutException => (
                StatusCodes.Status408RequestTimeout,
                "Request Timeout",
                "The operation timed out. Please try again."
            ),

            // Database/data errors
            Exception e when e.InnerException?.Message.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true => (
                StatusCodes.Status500InternalServerError,
                "Database Error",
                "A database error occurred. Please try again later."
            ),

            // Default - unhandled exception
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please try again later."
            )
        };
    }

    private static void LogException(
        Exception exception,
        int statusCode,
        string correlationId,
        ILogger<ExceptionHandlerMiddleware> logger,
        HttpContext context)
    {
        using (Serilog.Context.LogContext.PushProperty("correlation_id", correlationId))
        using (Serilog.Context.LogContext.PushProperty("exception_type", exception.GetType().Name))
        using (Serilog.Context.LogContext.PushProperty("http_status_code", statusCode))
        {
            if (statusCode >= 500)
            {
                logger.LogError(
                    exception,
                    "Unhandled exception occurred | Method={Method} Path={Path} StatusCode={StatusCode}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    statusCode);
            }
            else if (statusCode >= 400)
            {
                logger.LogWarning(
                    exception,
                    "Client error | Method={Method} Path={Path} StatusCode={StatusCode}",
                    context.Request.Method,
                    context.Request.Path.Value,
                    statusCode);
            }
        }
    }
}
