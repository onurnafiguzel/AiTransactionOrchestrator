using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Fraud.Worker.VelocityCheck;

/// <summary>
/// Redis-backed velocity check service for production use
/// Uses Redis STRING for counter and LIST for transaction details
/// </summary>
public sealed class RedisVelocityCheckService : IVelocityCheckService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisVelocityCheckService> _logger;
    
    private const string KeyPrefix = "velocity:rejected:";
    private const int CacheTtlMinutes = 10; // 10 dakikalık time window

    public RedisVelocityCheckService(
        IConnectionMultiplexer redis,
        ILogger<RedisVelocityCheckService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<int> GetRejectedTransactionCountAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{userId}:count";
            var value = await _db.StringGetAsync(key);

            if (value.HasValue && int.TryParse(value.ToString(), out var count))
            {
                _logger.LogDebug("Velocity check cache hit for user {UserId}: {Count}", userId, count);
                return count;
            }

            _logger.LogDebug("Velocity check cache miss for user {UserId}", userId);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rejected transaction count from Redis for user {UserId}", userId);
            return 0; // Fail-safe: don't block transaction
        }
    }

    public async Task RecordRejectedTransactionAsync(
        string userId,
        decimal amount,
        string merchant,
        string country,
        CancellationToken ct = default)
    {
        try
        {
            var countKey = $"{KeyPrefix}{userId}:count";
            var detailsKey = $"{KeyPrefix}{userId}:details";

            // Increment counter atomically
            var newCount = await _db.StringIncrementAsync(countKey);

            // Set expiration if it's a new key (first increment)
            if (newCount == 1)
            {
                await _db.KeyExpireAsync(countKey, TimeSpan.FromMinutes(CacheTtlMinutes));
            }

            // Store transaction details (optional, for audit)
            var details = $"{DateTime.UtcNow:O}|{amount}|{merchant}|{country}";
            await _db.ListRightPushAsync(detailsKey, details);
            await _db.KeyExpireAsync(detailsKey, TimeSpan.FromMinutes(CacheTtlMinutes));

            _logger.LogWarning(
                "Rejected transaction recorded in Redis for user {UserId}. Count: {Count}, Amount: {Amount}, Merchant: {Merchant}",
                userId, newCount, amount, merchant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording rejected transaction in Redis for user {UserId}", userId);
            // Non-blocking error - don't throw
        }
    }

    public async Task CleanupOldRecordsAsync(int ageInMinutes = 1440, CancellationToken ct = default)
    {
        try
        {
            // Redis otomatik TTL ile expiration yönetir
            // Bu method sadece logging için kullanılabilir
            var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{KeyPrefix}*");
            var keyCount = keys.Count();

            _logger.LogDebug("Velocity check cleanup check. Active keys: {KeyCount}", keyCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Redis velocity check cleanup");
            // Non-blocking error
        }
    }
}
