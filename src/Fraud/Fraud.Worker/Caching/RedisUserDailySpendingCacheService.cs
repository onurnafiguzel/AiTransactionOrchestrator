using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Caching;

/// <summary>
/// User başına günlük harcama takibi - Daily limit kontrolü için
/// </summary>
public interface IUserDailySpendingCacheService
{
    Task<decimal> GetDailySpentAsync(Guid userId, CancellationToken ct = default);
    Task<decimal> GetDailyLimitAsync(Guid userId, CancellationToken ct = default);
    Task AddDailySpendingAsync(Guid userId, decimal amount, CancellationToken ct = default);
    Task SetDailyLimitAsync(Guid userId, decimal limit, CancellationToken ct = default);
    Task ResetDailySpendingAsync(Guid userId, CancellationToken ct = default);
}

public sealed class RedisUserDailySpendingCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisUserDailySpendingCacheService> logger) : IUserDailySpendingCacheService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string SpentKeyPrefix = "user:daily:spent:";
    private const string LimitKeyPrefix = "user:daily:limit:";
    private const decimal DefaultDailyLimit = 100000; // 100K default
    private readonly TimeSpan _dailyTtl = TimeSpan.FromHours(24);

    public async Task<decimal> GetDailySpentAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var key = $"{SpentKeyPrefix}{userId}:{today}";
            var value = await _db.StringGetAsync(key);

            if (value.HasValue && decimal.TryParse(value.ToString(), out var spent))
            {
                logger.LogDebug("Daily spending cache hit for user {UserId}: {Spent}", userId, spent);
                return spent;
            }

            logger.LogDebug("Daily spending cache miss for user {UserId}", userId);
            return 0m;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting daily spending from Redis for user {UserId}", userId);
            return 0m; // Fail-safe
        }
    }

    public async Task<decimal> GetDailyLimitAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{LimitKeyPrefix}{userId}";
            var value = await _db.StringGetAsync(key);

            if (value.HasValue && decimal.TryParse(value.ToString(), out var limit))
            {
                logger.LogDebug("Daily limit cache hit for user {UserId}: {Limit}", userId, limit);
                return limit;
            }

            logger.LogDebug("Daily limit not found for user {UserId}, using default: {DefaultLimit}", 
                userId, DefaultDailyLimit);
            return DefaultDailyLimit;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting daily limit from Redis for user {UserId}", userId);
            return DefaultDailyLimit; // Fail-safe: use default
        }
    }

    public async Task AddDailySpendingAsync(Guid userId, decimal amount, CancellationToken ct = default)
    {
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var key = $"{SpentKeyPrefix}{userId}:{today}";

            var newValue = await _db.StringIncrementAsync(key, (double)amount);
            await _db.KeyExpireAsync(key, _dailyTtl);

            logger.LogDebug(
                "Daily spending added for user {UserId}: {Amount}, New total: {Total}",
                userId, amount, newValue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding daily spending to Redis for user {UserId}", userId);
        }
    }

    public async Task SetDailyLimitAsync(Guid userId, decimal limit, CancellationToken ct = default)
    {
        try
        {
            var key = $"{LimitKeyPrefix}{userId}";
            await _db.StringSetAsync(key, limit.ToString(), TimeSpan.FromDays(30));
            logger.LogInformation("Daily limit set for user {UserId}: {Limit}", userId, limit);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting daily limit in Redis for user {UserId}", userId);
        }
    }

    public async Task ResetDailySpendingAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var key = $"{SpentKeyPrefix}{userId}:{today}";
            await _db.KeyDeleteAsync(key);
            logger.LogInformation("Daily spending reset for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting daily spending in Redis for user {UserId}", userId);
        }
    }
}
