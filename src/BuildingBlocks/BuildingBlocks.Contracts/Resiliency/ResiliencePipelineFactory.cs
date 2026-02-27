using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace BuildingBlocks.Contracts.Resiliency;

/// <summary>
/// Centralized factory for creating resilience pipelines.
/// Provides pre-configured pipelines for common scenarios.
/// </summary>
public sealed class ResiliencePipelineFactory
{
    private readonly ILogger<ResiliencePipelineFactory> _logger;
    private readonly ResiliencePipeline _databasePipeline;
    private readonly ResiliencePipeline _redisPipeline;
    private readonly ResiliencePipeline<HttpResponseMessage> _httpPipeline;
    private readonly ResiliencePipeline _databaseTimeoutPipeline;
    private readonly ResiliencePipeline _redisTimeoutPipeline;
    private readonly ResiliencePipeline<HttpResponseMessage> _httpTimeoutPipeline;
    private readonly ResiliencePipeline _databaseRetryWithTimeoutPipeline;
    private readonly ResiliencePipeline _redisRetryWithTimeoutPipeline;
    private readonly ResiliencePipeline<HttpResponseMessage> _httpRetryWithTimeoutPipeline;

    public ResiliencePipelineFactory(ILogger<ResiliencePipelineFactory> logger)
    {
        _logger = logger;

        // Pre-create retry pipelines
        _databasePipeline = ResilienceExtensions.CreateDatabaseRetryPipeline(_logger);
        _redisPipeline = ResilienceExtensions.CreateRedisRetryPipeline(_logger);
        _httpPipeline = ResilienceExtensions.CreateHttpRetryPipeline(_logger);

        // Pre-create timeout pipelines
        _databaseTimeoutPipeline = ResilienceExtensions.CreateDatabaseTimeoutPipeline(_logger);
        _redisTimeoutPipeline = ResilienceExtensions.CreateRedisTimeoutPipeline(_logger);
        _httpTimeoutPipeline = ResilienceExtensions.CreateHttpTimeoutPipeline(_logger);

        // Pre-create combined retry + timeout pipelines
        _databaseRetryWithTimeoutPipeline = ResilienceExtensions.CreateDatabaseRetryWithTimeoutPipeline(_logger);
        _redisRetryWithTimeoutPipeline = ResilienceExtensions.CreateRedisRetryWithTimeoutPipeline(_logger);
        _httpRetryWithTimeoutPipeline = ResilienceExtensions.CreateHttpRetryWithTimeoutPipeline(_logger);

        _logger.LogInformation("ResiliencePipelineFactory initialized with retry, timeout, and combined policies");
    }

    /// <summary>
    /// Get or create a resilience pipeline for database operations.
    /// </summary>
    public ResiliencePipeline GetDatabasePipeline(DatabaseRetryOptions? options = null)
    {
        return options == null
            ? _databasePipeline
            : ResilienceExtensions.CreateDatabaseRetryPipeline(_logger, options);
    }

    /// <summary>
    /// Get or create a resilience pipeline for Redis operations.
    /// </summary>
    public ResiliencePipeline GetRedisPipeline(RedisRetryOptions? options = null)
    {
        return options == null
            ? _redisPipeline
            : ResilienceExtensions.CreateRedisRetryPipeline(_logger, options);
    }

    /// <summary>
    /// Get or create a resilience pipeline for HTTP operations.
    /// </summary>
    public ResiliencePipeline<HttpResponseMessage> GetHttpPipeline(HttpRetryOptions? options = null)
    {
        return options == null
            ? _httpPipeline
            : ResilienceExtensions.CreateHttpRetryPipeline(_logger, options);
    }

    // ==================== TIMEOUT PIPELINES ====================

    /// <summary>
    /// Get or create a timeout pipeline for database operations (default: 15s).
    /// </summary>
    public ResiliencePipeline GetDatabaseTimeoutPipeline(DatabaseTimeoutOptions? options = null)
    {
        return options == null
            ? _databaseTimeoutPipeline
            : ResilienceExtensions.CreateDatabaseTimeoutPipeline(_logger, options);
    }

    /// <summary>
    /// Get or create a timeout pipeline for Redis operations (default: 5s).
    /// </summary>
    public ResiliencePipeline GetRedisTimeoutPipeline(RedisTimeoutOptions? options = null)
    {
        return options == null
            ? _redisTimeoutPipeline
            : ResilienceExtensions.CreateRedisTimeoutPipeline(_logger, options);
    }

    /// <summary>
    /// Get or create a timeout pipeline for HTTP operations (default: 30s).
    /// </summary>
    public ResiliencePipeline<HttpResponseMessage> GetHttpTimeoutPipeline(HttpTimeoutOptions? options = null)
    {
        return options == null
            ? _httpTimeoutPipeline
            : ResilienceExtensions.CreateHttpTimeoutPipeline(_logger, options);
    }

    // ==================== COMBINED PIPELINES (Retry + Timeout) ====================

    /// <summary>
    /// Get or create a combined retry + timeout pipeline for database operations.
    /// Timeout is applied per retry attempt.
    /// </summary>
    public ResiliencePipeline GetDatabaseRetryWithTimeoutPipeline(
        DatabaseRetryOptions? retryOptions = null,
        DatabaseTimeoutOptions? timeoutOptions = null)
    {
        return retryOptions == null && timeoutOptions == null
            ? _databaseRetryWithTimeoutPipeline
            : ResilienceExtensions.CreateDatabaseRetryWithTimeoutPipeline(_logger, retryOptions, timeoutOptions);
    }

    /// <summary>
    /// Get or create a combined retry + timeout pipeline for Redis operations.
    /// Timeout is applied per retry attempt.
    /// </summary>
    public ResiliencePipeline GetRedisRetryWithTimeoutPipeline(
        RedisRetryOptions? retryOptions = null,
        RedisTimeoutOptions? timeoutOptions = null)
    {
        return retryOptions == null && timeoutOptions == null
            ? _redisRetryWithTimeoutPipeline
            : ResilienceExtensions.CreateRedisRetryWithTimeoutPipeline(_logger, retryOptions, timeoutOptions);
    }

    /// <summary>
    /// Get or create a combined retry + timeout pipeline for HTTP operations.
    /// Timeout is applied per retry attempt.
    /// </summary>
    public ResiliencePipeline<HttpResponseMessage> GetHttpRetryWithTimeoutPipeline(
        HttpRetryOptions? retryOptions = null,
        HttpTimeoutOptions? timeoutOptions = null)
    {
        return retryOptions == null && timeoutOptions == null
            ? _httpRetryWithTimeoutPipeline
            : ResilienceExtensions.CreateHttpRetryWithTimeoutPipeline(_logger, retryOptions, timeoutOptions);
    }

    // ==================== CUSTOM PIPELINE ====================

    /// <summary>
    /// Create a custom resilience pipeline with specific options.
    /// </summary>
    public ResiliencePipeline CreateCustomPipeline(
        Func<Exception, bool> shouldHandle,
        RetryOptions options)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(shouldHandle),
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(options.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = options.UseJitter,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Custom retry attempt {Attempt} after {Delay}ms. Exception: {ExceptionType}: {Message}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.GetType().Name,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
