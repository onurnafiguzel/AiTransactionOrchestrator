using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Transaction.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for comprehensive request/response logging.
/// Logs command/query execution details before and after handler execution.
/// Uses primary constructor pattern for modern C# 12+.
/// </summary>
/// <typeparam name="TRequest">The request type (Command or Query)</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = ExtractCorrelationId(request);

        // Log request start
        LogRequestStart(requestName, correlationId, request);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();

            stopwatch.Stop();

            // Log request success
            LogRequestSuccess(requestName, correlationId, response, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log request failure
            LogRequestFailure(requestName, correlationId, ex, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    /// <summary>
    /// Logs request execution start with command/query name, correlation ID, and all properties.
    /// </summary>
    private void LogRequestStart(string requestName, string correlationId, TRequest request)
    {
        var requestData = SerializeRequest(request);

        logger.LogInformation(
            "⏱️  REQUEST STARTED | Name: {RequestName} | CorrelationId: {CorrelationId} | Data: {Data}",
            requestName,
            correlationId,
            requestData);
    }

    /// <summary>
    /// Logs successful request execution with duration and response data.
    /// </summary>
    private void LogRequestSuccess(string requestName, string correlationId, TResponse response, long elapsedMs)
    {
        var responseData = SerializeResponse(response);

        logger.LogInformation(
            "✅ REQUEST SUCCEEDED | Name: {RequestName} | CorrelationId: {CorrelationId} | " +
            "Duration: {DurationMs}ms | Response: {Response}",
            requestName,
            correlationId,
            elapsedMs,
            responseData);
    }

    /// <summary>
    /// Logs failed request execution with exception details and duration.
    /// </summary>
    private void LogRequestFailure(
        string requestName,
        string correlationId,
        Exception exception,
        long elapsedMs)
    {
        logger.LogError(
            exception,
            "❌ REQUEST FAILED | Name: {RequestName} | CorrelationId: {CorrelationId} | " +
            "Duration: {DurationMs}ms | ExceptionType: {ExceptionType} | Message: {Message}",
            requestName,
            correlationId,
            elapsedMs,
            exception.GetType().Name,
            exception.Message);
    }

    /// <summary>
    /// Serializes request object to JSON string with all public properties.
    /// Excludes sensitive properties (password, secret, token, etc).
    /// </summary>
    private string SerializeRequest(TRequest request)
    {
        try
        {
            var properties = typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var data = new Dictionary<string, object?>();

            foreach (var prop in properties)
            {
                var propertyName = prop.Name;
                var value = prop.GetValue(request);

                // Redact sensitive properties
                if (IsSensitiveProperty(propertyName))
                {
                    data[propertyName] = "***REDACTED***";
                }
                else
                {
                    data[propertyName] = value ?? "null";
                }
            }

            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return JsonSerializer.Serialize(data, serializeOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to serialize request data");
            return request.ToString() ?? "Unable to serialize";
        }
    }

    /// <summary>
    /// Serializes response object to JSON string.
    /// Handles primitive types and complex objects.
    /// </summary>
    private string SerializeResponse(TResponse? response)
    {
        try
        {
            if (response == null)
                return "null";

            var responseType = typeof(TResponse);

            // Handle primitive types
            if (responseType.IsPrimitive || responseType == typeof(string) || responseType == typeof(Guid))
            {
                return response.ToString() ?? "null";
            }

            // Handle complex types
            var serializeOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return JsonSerializer.Serialize(response, serializeOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to serialize response data");
            return response?.ToString() ?? "Unable to serialize";
        }
    }

    /// <summary>
    /// Extracts correlation ID from request object by property name.
    /// </summary>
    private static string ExtractCorrelationId(TRequest request)
    {
        var correlationIdProperty = typeof(TRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            .FirstOrDefault(p => p.Name.Equals("CorrelationId", StringComparison.OrdinalIgnoreCase));

        if (correlationIdProperty?.GetValue(request) is string correlationId && !string.IsNullOrEmpty(correlationId))
        {
            return correlationId;
        }

        // Fallback to request type name
        return typeof(TRequest).Name;
    }

    /// <summary>
    /// Checks if property name contains sensitive keywords to redact from logs.
    /// </summary>
    private static bool IsSensitiveProperty(string propertyName)
    {
        var sensitiveKeywords = new[]
        {
            "password",
            "secret",
            "token",
            "apikey",
            "authorization",
            "credential",
            "private"
        };

        return sensitiveKeywords.Any(keyword =>
            propertyName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}