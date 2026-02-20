using StackExchange.Redis;

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
            var ttl = TimeSpan.FromMinutes(CacheTtlMinutes);

            // Increment counter atomically
            var newCount = await _db.StringIncrementAsync(countKey);

            // ALWAYS update TTL (even if key already existed)
            // Bu şekilde, her yeni rejection'da 10 dakika timer reset olur
            await _db.KeyExpireAsync(countKey, ttl);

            // Store transaction details
            var details = $"{DateTime.UtcNow:O}|{amount}|{merchant}|{country}";
            await _db.ListRightPushAsync(detailsKey, details);

            // Details list'inin TTL'ini de her zaman set et
            await _db.KeyExpireAsync(detailsKey, ttl);

            _logger.LogWarning(
                "Rejected transaction recorded in Redis for user {UserId}. Count: {Count}, Amount: {Amount}, Merchant: {Merchant}, TTL: {Ttl}m",
                userId, newCount, amount, merchant, CacheTtlMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording rejected transaction in Redis for user {UserId}", userId);
        }
    }

    public async Task CleanupOldRecordsAsync(int ageInMinutes = 1440, CancellationToken ct = default)
    {
        try
        {
            var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{KeyPrefix}*:details");

            var deletedCount = 0;
            var cutoffTime = DateTime.UtcNow.AddMinutes(-ageInMinutes);

            foreach (var key in keys)
            {
                try
                {
                    // LIST'teki tüm detailları al
                    var details = await _db.ListRangeAsync(key);

                    if (details.Length == 0)
                    {
                        // Boş list'i sil
                        await _db.KeyDeleteAsync(key);
                        deletedCount++;
                        continue;
                    }

                    // İlk item'in timestamp'ini kontrol et (oldest)
                    var oldestDetail = details.First().ToString();
                    if (string.IsNullOrEmpty(oldestDetail))
                        continue;

                    var parts = oldestDetail.Split('|');
                    if (parts.Length == 0 || !DateTime.TryParse(parts[0], out var timestamp))
                        continue;

                    // Eğer en eski kayıt cutoff time'dan önceyse, tüm list'i sil
                    if (timestamp < cutoffTime)
                    {
                        await _db.KeyDeleteAsync(key);

                        // Karşılık gelen count key'ini de sil
                        var countKey = key.ToString().Replace(":details", ":count");
                        await _db.KeyDeleteAsync(countKey);

                        deletedCount++;
                        _logger.LogInformation(
                            "Cleaned up old velocity records. Key: {Key}, OldestRecord: {Timestamp}",
                            key, timestamp);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing key {Key} during cleanup", key);
                    // Continue with next key
                }
            }

            _logger.LogInformation(
                "Velocity check cleanup completed. Deleted {DeletedCount} user records older than {AgeMinutes} minutes",
                deletedCount, ageInMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Redis velocity check cleanup");
            // Non-blocking error
        }
    }
}
