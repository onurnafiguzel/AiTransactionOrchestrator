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

    public ResiliencePipelineFactory(ILogger<ResiliencePipelineFactory> logger)
    {
        _logger = logger;

        // Pre-create commonly used pipelines
        _databasePipeline = ResilienceExtensions.CreateDatabaseRetryPipeline(_logger);
        _redisPipeline = ResilienceExtensions.CreateRedisRetryPipeline(_logger);
        _httpPipeline = ResilienceExtensions.CreateHttpRetryPipeline(_logger);

        _logger.LogInformation("ResiliencePipelineFactory initialized with default retry policies");
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
