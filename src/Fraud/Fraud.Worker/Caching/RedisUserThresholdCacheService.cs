using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Caching;

/// <summary>
/// User başına threshold (maximum transaction amount) yönetimi
/// Premium kullanıcılar daha yüksek limitlerde işlem yapabilir
/// </summary>
public interface IUserThresholdCacheService
{
    Task<decimal?> GetUserThresholdAsync(Guid userId, CancellationToken ct = default);
    Task SetUserThresholdAsync(Guid userId, decimal threshold, CancellationToken ct = default);
    Task DeleteUserThresholdAsync(Guid userId, CancellationToken ct = default);
}

public sealed class RedisUserThresholdCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisUserThresholdCacheService> logger) : IUserThresholdCacheService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string KeyPrefix = "user:threshold:";
    private readonly TimeSpan _ttl = TimeSpan.FromDays(30); // 30 günlük cache

    public async Task<decimal?> GetUserThresholdAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{userId}";
            var value = await _db.StringGetAsync(key);

            if (value.HasValue && decimal.TryParse(value.ToString(), out var threshold))
            {
                logger.LogDebug("User threshold cache hit for user {UserId}: {Threshold}", userId, threshold);
                return threshold;
            }

            logger.LogDebug("User threshold cache miss for user {UserId}", userId);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user threshold from Redis for user {UserId}", userId);
            return null; // Fail-safe
        }
    }

    public async Task SetUserThresholdAsync(Guid userId, decimal threshold, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{userId}";
            await _db.StringSetAsync(key, threshold.ToString(), _ttl);
            logger.LogInformation("User threshold set for user {UserId}: {Threshold}", userId, threshold);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting user threshold in Redis for user {UserId}", userId);
        }
    }

    public async Task DeleteUserThresholdAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{userId}";
            await _db.KeyDeleteAsync(key);
            logger.LogInformation("User threshold deleted for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting user threshold from Redis for user {UserId}", userId);
        }
    }
}
