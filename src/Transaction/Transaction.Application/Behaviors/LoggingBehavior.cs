using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Transaction.Application.Behaviors;

/// <summary>
/// Logging behavior for MediatR pipeline.
/// Logs command/query execution with full details before and after handler execution.
/// </summary>
/// <typeparam name="TRequest">The request type (Command or Query)</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = GetCorrelationId(request);

        // Log before handler execution
        LogRequestStart(requestName, requestId, request);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();

            stopwatch.Stop();

            // Log after successful handler execution
            LogRequestSuccess(requestName, requestId, response, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log after failed handler execution
            LogRequestFailure(requestName, requestId, ex, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private void LogRequestStart(string requestName, string requestId, TRequest request)
    {
        var requestData = SerializeRequest(request);

        _logger.LogInformation(
            "Starting request execution. RequestName: {RequestName}, CorrelationId: {CorrelationId}, Data: {Data}",
            requestName,
            requestId,
            requestData);
    }

    private void LogRequestSuccess(string requestName, string requestId, TResponse response, long elapsedMs)
    {
        var responseData = SerializeResponse(response);

        _logger.LogInformation(
            "Request completed successfully. RequestName: {RequestName}, CorrelationId: {CorrelationId}, Duration: {DurationMs}ms, Response: {Response}",
            requestName,
            requestId,
            elapsedMs,
            responseData);
    }

    private void LogRequestFailure(string requestName, string requestId, Exception exception, long elapsedMs)
    {
        _logger.LogError(
            exception,
            "Request failed. RequestName: {RequestName}, CorrelationId: {CorrelationId}, Duration: {DurationMs}ms, ExceptionType: {ExceptionType}",
            requestName,
            requestId,
            elapsedMs,
            exception.GetType().Name);
    }

    private string SerializeRequest(TRequest request)
    {
        try
        {
            var properties = typeof(TRequest).GetProperties();
            var data = new Dictionary<string, object>();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(request);

                // Don't log sensitive data like passwords
                if (IsSensitiveProperty(prop.Name))
                {
                    data[prop.Name] = "***REDACTED***";
                }
                else
                {
                    data[prop.Name] = value ?? "null";
                }
            }

            return JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize request data");
            return request.ToString() ?? "Unable to serialize";
        }
    }

    private string SerializeResponse(TResponse response)
    {
        try
        {
            if (response == null)
                return "null";

            var responseType = typeof(TResponse);

            // For simple types
            if (responseType.IsPrimitive || responseType == typeof(string))
            {
                return response.ToString() ?? "null";
            }

            // For complex types
            return JsonSerializer.Serialize(response, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize response data");
            return response?.ToString() ?? "Unable to serialize";
        }
    }

    private string GetCorrelationId(TRequest request)
    {
        var correlationIdProperty = typeof(TRequest)
            .GetProperties()
            .FirstOrDefault(p => p.Name.Equals("CorrelationId", StringComparison.OrdinalIgnoreCase));

        if (correlationIdProperty?.GetValue(request) is string correlationId)
        {
            return correlationId;
        }

        // Fallback to request name
        return typeof(TRequest).Name;
    }

    private static bool IsSensitiveProperty(string propertyName)
    {
        var sensitiveKeywords = new[] 
        { 
            "password", 
            "secret", 
            "token", 
            "key", 
            "credential",
            "apikey",
            "authorization"
        };

        return sensitiveKeywords.Any(keyword => 
            propertyName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}