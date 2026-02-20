using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace BuildingBlocks.Contracts.Resiliency;

/// <summary>
/// Extension methods for building resilience pipelines with retry policies.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Create an exponential backoff retry pipeline with jitter for database operations.
    /// Handles transient database errors (connection, timeout, deadlock).
    /// </summary>
    public static ResiliencePipeline CreateDatabaseRetryPipeline(
        ILogger logger,
        DatabaseRetryOptions? options = null)
    {
        options ??= new DatabaseRetryOptions();

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    IsDatabaseTransientError(ex)),
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(options.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Database operation retry attempt {Attempt} after {Delay}ms. Exception: {ExceptionType}: {Message}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Create an exponential backoff retry pipeline for Redis operations.
    /// Handles transient Redis errors (connection, timeout).
    /// </summary>
    public static ResiliencePipeline CreateRedisRetryPipeline(
        ILogger logger,
        RedisRetryOptions? options = null)
    {
        options ??= new RedisRetryOptions();

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    IsRedisTransientError(ex)),
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(options.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Redis operation retry attempt {Attempt} after {Delay}ms. Exception: {ExceptionType}: {Message}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Create an exponential backoff retry pipeline for HTTP operations.
    /// Handles transient HTTP errors (5xx, timeouts, network errors).
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpRetryPipeline(
        ILogger logger,
        HttpRetryOptions? options = null)
    {
        options ??= new HttpRetryOptions();

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(response =>
                        (int)response.StatusCode >= 500 || // Server errors
                        response.StatusCode == System.Net.HttpStatusCode.RequestTimeout || // 408
                        response.StatusCode == System.Net.HttpStatusCode.TooManyRequests), // 429
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(options.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                OnRetry = args =>
                {
                    var statusCode = args.Outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exType = args.Outcome.Exception?.GetType().Name ?? "N/A";
                    
                    logger.LogWarning(
                        "HTTP operation retry attempt {Attempt} after {Delay}ms. StatusCode: {StatusCode}, Exception: {ExceptionType}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        statusCode,
                        exType);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Create decorated exponential backoff delays with jitter.
    /// Uses Polly.Contrib.WaitAndRetry for more sophisticated backoff strategies.
    /// </summary>
    public static IEnumerable<TimeSpan> DecorrelatedJitterBackoff(
        int maxRetries,
        TimeSpan seedDelay,
        TimeSpan maxDelay)
    {
        return Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: seedDelay,
            retryCount: maxRetries,
            fastFirst: true);
    }

    // Helper methods for transient error detection

    private static bool IsDatabaseTransientError(Exception ex)
    {
        var errorMessage = ex.Message.ToLowerInvariant();
        var exceptionType = ex.GetType().Name.ToLowerInvariant();

        return exceptionType.Contains("timeout") ||
               exceptionType.Contains("connection") ||
               exceptionType.Contains("network") ||
               exceptionType.Contains("deadlock") ||
               errorMessage.Contains("timeout") ||
               errorMessage.Contains("connection") ||
               errorMessage.Contains("deadlock") ||
               errorMessage.Contains("cannot open database") ||
               errorMessage.Contains("unavailable") ||
               errorMessage.Contains("too many connections") ||
               errorMessage.Contains("broken pipe");
    }

    private static bool IsRedisTransientError(Exception ex)
    {
        var errorMessage = ex.Message.ToLowerInvariant();
        var exceptionType = ex.GetType().Name.ToLowerInvariant();

        return exceptionType.Contains("timeout") ||
               exceptionType.Contains("connection") ||
               exceptionType.Contains("redis") ||
               errorMessage.Contains("timeout") ||
               errorMessage.Contains("connection") ||
               errorMessage.Contains("no connection") ||
               errorMessage.Contains("socket") ||
               errorMessage.Contains("unavailable");
    }
}
