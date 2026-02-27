using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using Polly.Timeout;

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

    // ==================== TIMEOUT PIPELINES ====================

    /// <summary>
    /// Create a timeout pipeline for database operations.
    /// Prevents hanging database queries from blocking threads indefinitely.
    /// </summary>
    public static ResiliencePipeline CreateDatabaseTimeoutPipeline(
        ILogger logger,
        DatabaseTimeoutOptions? options = null)
    {
        options ??= new DatabaseTimeoutOptions();

        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Database operation timed out after {Timeout}s",
                        args.Timeout.TotalSeconds);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Create a timeout pipeline for Redis operations.
    /// Prevents hanging cache calls from blocking the request pipeline.
    /// </summary>
    public static ResiliencePipeline CreateRedisTimeoutPipeline(
        ILogger logger,
        RedisTimeoutOptions? options = null)
    {
        options ??= new RedisTimeoutOptions();

        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Redis operation timed out after {Timeout}s",
                        args.Timeout.TotalSeconds);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Create a timeout pipeline for HTTP operations.
    /// Prevents slow external API calls from blocking the system.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpTimeoutPipeline(
        ILogger logger,
        HttpTimeoutOptions? options = null)
    {
        options ??= new HttpTimeoutOptions();

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "HTTP operation timed out after {Timeout}s",
                        args.Timeout.TotalSeconds);
                    return default;
                }
            })
            .Build();
    }

    // ==================== COMBINED PIPELINES (Retry + Timeout) ====================

    /// <summary>
    /// Create a combined retry + timeout pipeline for database operations.
    /// Timeout wraps each individual retry attempt.
    /// </summary>
    public static ResiliencePipeline CreateDatabaseRetryWithTimeoutPipeline(
        ILogger logger,
        DatabaseRetryOptions? retryOptions = null,
        DatabaseTimeoutOptions? timeoutOptions = null)
    {
        retryOptions ??= new DatabaseRetryOptions();
        timeoutOptions ??= new DatabaseTimeoutOptions();

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => IsDatabaseTransientError(ex))
                    .Handle<TimeoutRejectedException>(),
                MaxRetryAttempts = retryOptions.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = retryOptions.UseJitter,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Database retry+timeout attempt {Attempt} after {Delay}ms. Exception: {ExceptionType}: {Message}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Database operation timed out after {Timeout}s (per-attempt timeout)",
                        args.Timeout.TotalSeconds);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Create a combined retry + timeout pipeline for Redis operations.
    /// Timeout wraps each individual retry attempt.
    /// </summary>
    public static ResiliencePipeline CreateRedisRetryWithTimeoutPipeline(
        ILogger logger,
        RedisRetryOptions? retryOptions = null,
        RedisTimeoutOptions? timeoutOptions = null)
    {
        retryOptions ??= new RedisRetryOptions();
        timeoutOptions ??= new RedisTimeoutOptions();

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => IsRedisTransientError(ex))
                    .Handle<TimeoutRejectedException>(),
                MaxRetryAttempts = retryOptions.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = retryOptions.UseJitter,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Redis retry+timeout attempt {Attempt} after {Delay}ms. Exception: {ExceptionType}: {Message}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Redis operation timed out after {Timeout}s (per-attempt timeout)",
                        args.Timeout.TotalSeconds);
                    return default;
                }
            })
            .Build();
    }

    /// <summary>
    /// Create a combined retry + timeout pipeline for HTTP operations.
    /// Timeout wraps each individual retry attempt.
    /// </summary>
    public static ResiliencePipeline<HttpResponseMessage> CreateHttpRetryWithTimeoutPipeline(
        ILogger logger,
        HttpRetryOptions? retryOptions = null,
        HttpTimeoutOptions? timeoutOptions = null)
    {
        retryOptions ??= new HttpRetryOptions();
        timeoutOptions ??= new HttpTimeoutOptions();

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(response =>
                        (int)response.StatusCode >= 500 ||
                        response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                        response.StatusCode == System.Net.HttpStatusCode.TooManyRequests),
                MaxRetryAttempts = retryOptions.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(retryOptions.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = retryOptions.UseJitter,
                OnRetry = args =>
                {
                    var statusCode = args.Outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exType = args.Outcome.Exception?.GetType().Name ?? "N/A";

                    logger.LogWarning(
                        "HTTP retry+timeout attempt {Attempt} after {Delay}ms. StatusCode: {StatusCode}, Exception: {ExceptionType}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        statusCode,
                        exType);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(timeoutOptions.TimeoutSeconds),
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "HTTP operation timed out after {Timeout}s (per-attempt timeout)",
                        args.Timeout.TotalSeconds);
                    return default;
                }
            })
            .Build();
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
