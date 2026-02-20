using BuildingBlocks.Contracts.Observability;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Transaction.Api.Middleware;

/// <summary>
/// Middleware for comprehensive HTTP request/response logging.
/// Logs incoming requests with all details and outgoing responses with durations.
/// Uses primary constructor pattern for modern C# 12+.
/// </summary>
public sealed class RequestResponseLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestResponseLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = GetCorrelationId(context);
        var requestId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            // Log incoming request
            await LogRequestAsync(context, correlationId, requestId);

            // Capture response stream
            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                try
                {
                    await next(context);
                }
                finally
                {
                    stopwatch.Stop();

                    // Log outgoing response
                    await LogResponseAsync(
                        context,
                        responseBody,
                        originalBodyStream,
                        correlationId,
                        requestId,
                        stopwatch.ElapsedMilliseconds);
                }
            }

            context.Response.Body = originalBodyStream;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "[{RequestId}] Unhandled exception. CorrelationId: {CorrelationId}, Duration: {DurationMs}ms",
                requestId,
                correlationId,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Logs incoming HTTP request with method, path, query, body, and client IP.
    /// </summary>
    private async Task LogRequestAsync(HttpContext context, string correlationId, string requestId)
    {
        var request = context.Request;
        var method = request.Method;
        var path = request.Path.Value;
        var queryString = request.QueryString.Value;

        string requestBody = string.Empty;

        // Only capture body for POST, PUT, PATCH
        if (request.ContentLength > 0 && IsWriteMethod(method))
        {
            request.EnableBuffering();
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }
        }

        var clientIp = context.Items["ClientIpAddress"]?.ToString() ?? "Unknown";

        logger.LogInformation(
            "[{RequestId}] ▶️  INCOMING REQUEST | CorrelationId: {CorrelationId} | " +
            "Method: {Method} | Path: {Path} | Query: {Query} | ClientIp: {ClientIp} | " +
            "ContentLength: {ContentLength}{RequestBodyLog}",
            requestId,
            correlationId,
            method,
            path,
            string.IsNullOrEmpty(queryString) ? "N/A" : queryString,
            clientIp,
            request.ContentLength ?? 0,
            !string.IsNullOrEmpty(requestBody) ? $" | Body: {Sanitize(requestBody)}" : "");
    }

    /// <summary>
    /// Logs outgoing HTTP response with status code, duration, and response body.
    /// Color codes: ✅ = success, ⚠️ = client error, ❌ = server error.
    /// </summary>
    private async Task LogResponseAsync(
        HttpContext context,
        MemoryStream responseBody,
        Stream originalBodyStream,
        string correlationId,
        string requestId,
        long durationMs)
    {
        var response = context.Response;
        var statusCode = response.StatusCode;

        responseBody.Seek(0, SeekOrigin.Begin);
        var responseBodyText = await new StreamReader(responseBody).ReadToEndAsync();
        responseBody.Seek(0, SeekOrigin.Begin);

        // Copy response to original stream
        await responseBody.CopyToAsync(originalBodyStream);

        var logLevel = DetermineLogLevel(statusCode);
        var statusPrefix = statusCode switch
        {
            >= 200 and < 300 => "✅",
            >= 400 and < 500 => "⚠️ ",
            >= 500 => "❌",
            _ => "ℹ️ "
        };

        logger.Log(
            logLevel,
            "[{RequestId}] {StatusPrefix} OUTGOING RESPONSE | CorrelationId: {CorrelationId} | " +
            "StatusCode: {StatusCode} | Duration: {DurationMs}ms{ResponseBodyLog}",
            requestId,
            statusPrefix,
            correlationId,
            statusCode,
            durationMs,
            !string.IsNullOrEmpty(responseBodyText) ? $" | Body: {Sanitize(responseBodyText)}" : "");
    }

    /// <summary>
    /// Extracts correlation ID from context items or X-Correlation-Id header.
    /// Generates new ID if not found.
    /// </summary>
    private static string GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(Correlation.HeaderName, out var correlationId))
        {
            return correlationId?.ToString() ?? GenerateId();
        }

        if (context.Request.Headers.TryGetValue(Correlation.HeaderName, out var headerValue))
        {
            return headerValue.ToString();
        }

        return GenerateId();
    }

    /// <summary>
    /// Sanitizes JSON/XML content for logging (truncates and validates format).
    /// </summary>
    private static string Sanitize(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "N/A";

        const int maxLength = 500;
        var truncated = content.Length > maxLength ? content[..maxLength] + "... (truncated)" : content;

        try
        {
            // Validate JSON format
            JsonDocument.Parse(truncated);
            return truncated;
        }
        catch
        {
            // Not valid JSON, return as is
            return truncated;
        }
    }

    /// <summary>
    /// Determines appropriate log level based on HTTP status code.
    /// </summary>
    private static LogLevel DetermineLogLevel(int statusCode) =>
        statusCode switch
        {
            >= 200 and < 300 => LogLevel.Information,    // Success
            >= 300 and < 400 => LogLevel.Debug,           // Redirect
            >= 400 and < 500 => LogLevel.Warning,         // Client error
            >= 500 => LogLevel.Error,                     // Server error
            _ => LogLevel.Information
        };

    /// <summary>
    /// Checks if HTTP method is a write operation (POST, PUT, PATCH, DELETE).
    /// </summary>
    private static bool IsWriteMethod(string method) =>
        method is "POST" or "PUT" or "PATCH" or "DELETE";

    /// <summary>
    /// Generates a short unique ID for request tracking.
    /// </summary>
    private static string GenerateId() =>
        Guid.NewGuid().ToString("N")[..8];
}
