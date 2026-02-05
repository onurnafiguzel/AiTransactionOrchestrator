using StackExchange.Redis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Transaction.Infrastructure.Caching;

/// <summary>
/// Redis-backed transaction caching service
/// </summary>
public interface ITransactionCacheService
{
    Task SetTransactionAsync<T>(Guid transactionId, T data, int ttlMinutes = 10, CancellationToken ct = default);
    Task<T?> GetTransactionAsync<T>(Guid transactionId, CancellationToken ct = default);
    Task InvalidateTransactionAsync(Guid transactionId, CancellationToken ct = default);
}

public sealed class RedisTransactionCacheService : ITransactionCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisTransactionCacheService> _logger;
    
    private const string KeyPrefix = "transaction:";

    public RedisTransactionCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisTransactionCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task SetTransactionAsync<T>(
        Guid transactionId,
        T data,
        int ttlMinutes = 10,
        CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{transactionId}";
            var json = JsonSerializer.Serialize(data);
            
            await _db.StringSetAsync(key, json, TimeSpan.FromMinutes(ttlMinutes));
            _logger.LogDebug("Transaction {TransactionId} cached for {TtlMinutes} minutes", transactionId, ttlMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching transaction {TransactionId}", transactionId);
        }
    }

    public async Task<T?> GetTransactionAsync<T>(Guid transactionId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{transactionId}";
            var value = await _db.StringGetAsync(key);

            if (value.HasValue)
            {
                var data = JsonSerializer.Deserialize<T>(value.ToString());
                _logger.LogDebug("Transaction {TransactionId} found in cache", transactionId);
                return data;
            }

            _logger.LogDebug("Transaction {TransactionId} cache miss", transactionId);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transaction {TransactionId} from cache", transactionId);
            return default;
        }
    }

    public async Task InvalidateTransactionAsync(Guid transactionId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{transactionId}";
            await _db.KeyDeleteAsync(key);
            _logger.LogInformation("Transaction {TransactionId} cache invalidated", transactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating transaction {TransactionId} cache", transactionId);
        }
    }
}
