namespace BuildingBlocks.Contracts.Resiliency;

/// <summary>
/// Configuration options for retry policies.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Initial delay before first retry (in seconds).
    /// </summary>
    public double InitialDelaySeconds { get; init; } = 1.0;

    /// <summary>
    /// Maximum delay between retries (in seconds).
    /// </summary>
    public double MaxDelaySeconds { get; init; } = 30.0;

    /// <summary>
    /// Enable jitter to avoid thundering herd problem.
    /// </summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>
    /// Backoff multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;
}

/// <summary>
/// Retry options specifically for database operations.
/// </summary>
public sealed class DatabaseRetryOptions : RetryOptions
{
    public DatabaseRetryOptions()
    {
        MaxRetryAttempts = 3;
        InitialDelaySeconds = 0.5;
        MaxDelaySeconds = 10.0;
        UseJitter = true;
    }
}

/// <summary>
/// Retry options specifically for Redis operations.
/// </summary>
public sealed class RedisRetryOptions : RetryOptions
{
    public RedisRetryOptions()
    {
        MaxRetryAttempts = 2;
        InitialDelaySeconds = 0.2;
        MaxDelaySeconds = 5.0;
        UseJitter = true;
    }
}

/// <summary>
/// Retry options specifically for HTTP/API operations.
/// </summary>
public sealed class HttpRetryOptions : RetryOptions
{
    public HttpRetryOptions()
    {
        MaxRetryAttempts = 3;
        InitialDelaySeconds = 1.0;
        MaxDelaySeconds = 30.0;
        UseJitter = true;
    }
}

/// <summary>
/// Retry options specifically for message queue operations.
/// </summary>
public sealed class MessageQueueRetryOptions : RetryOptions
{
    public MessageQueueRetryOptions()
    {
        MaxRetryAttempts = 5;
        InitialDelaySeconds = 2.0;
        MaxDelaySeconds = 60.0;
        UseJitter = true;
    }
}
