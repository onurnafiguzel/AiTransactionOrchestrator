using BuildingBlocks.Contracts.Resiliency;
using Polly;
using StackExchange.Redis;
using System.Text.Json;

namespace Support.Bot.Caching;

/// <summary>
/// Redis-backed support transaction caching service
/// </summary>
public interface ISupportTransactionCacheService
{
    Task SetSupportTransactionAsync<T>(Guid transactionId, T data, int ttlMinutes = 10, CancellationToken ct = default);
    Task<T?> GetSupportTransactionAsync<T>(Guid transactionId, CancellationToken ct = default);
    Task InvalidateSupportTransactionAsync(Guid transactionId, CancellationToken ct = default);

    Task SetIncidentSummaryAsync<T>(string cacheKey, T data, int ttlMinutes = 30, CancellationToken ct = default);
    Task<T?> GetIncidentSummaryAsync<T>(string cacheKey, CancellationToken ct = default);
    Task InvalidateIncidentSummaryAsync(string cacheKey, CancellationToken ct = default);
}

public sealed class RedisSupportTransactionCacheService : ISupportTransactionCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisSupportTransactionCacheService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    private const string TransactionKeyPrefix = "support:transaction:";
    private const string IncidentSummaryKey = "support:incident:summary";

    public RedisSupportTransactionCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisSupportTransactionCacheService> logger,
        ResiliencePipelineFactory pipelineFactory)
    {
        _db = redis.GetDatabase();
        _logger = logger;
        _resiliencePipeline = pipelineFactory.GetRedisPipeline();
    }

    public async Task SetSupportTransactionAsync<T>(
        Guid transactionId,
        T data,
        int ttlMinutes = 10,
        CancellationToken ct = default)
    {
        try
        {
            await _resiliencePipeline.ExecuteAsync(async token =>
            {
                var key = $"{TransactionKeyPrefix}{transactionId}";
                var json = JsonSerializer.Serialize(data);

                await _db.StringSetAsync(key, json, TimeSpan.FromMinutes(ttlMinutes));
                _logger.LogDebug("Support transaction {TransactionId} cached for {TtlMinutes} minutes", transactionId, ttlMinutes);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching support transaction {TransactionId}", transactionId);
        }
    }

    public async Task<T?> GetSupportTransactionAsync<T>(Guid transactionId, CancellationToken ct = default)
    {
        try
        {
            return await _resiliencePipeline.ExecuteAsync(async token =>
            {
                var key = $"{TransactionKeyPrefix}{transactionId}";
                var value = await _db.StringGetAsync(key);

                if (value.HasValue)
                {
                    var data = JsonSerializer.Deserialize<T>(value.ToString());
                    _logger.LogDebug("Support transaction {TransactionId} found in cache", transactionId);
                    return data;
                }

                _logger.LogDebug("Support transaction {TransactionId} cache miss", transactionId);
                return default;
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving support transaction {TransactionId} from cache", transactionId);
            return default;
        }
    }

    public async Task InvalidateSupportTransactionAsync(Guid transactionId, CancellationToken ct = default)
    {
        try
        {
            await _resiliencePipeline.ExecuteAsync(async token =>
            {
                var key = $"{TransactionKeyPrefix}{transactionId}";
                await _db.KeyDeleteAsync(key);
                _logger.LogInformation("Support transaction {TransactionId} cache invalidated", transactionId);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating support transaction {TransactionId} cache", transactionId);
        }
    }

    public async Task SetIncidentSummaryAsync<T>(
        string cacheKey,
        T data,
        int ttlMinutes = 30,
        CancellationToken ct = default)
    {
        try
        {
            await _resiliencePipeline.ExecuteAsync(async token =>
            {
                var json = JsonSerializer.Serialize(data);
                await _db.StringSetAsync(cacheKey, json, TimeSpan.FromMinutes(ttlMinutes));
                _logger.LogDebug("Incident summary {CacheKey} cached for {TtlMinutes} minutes", cacheKey, ttlMinutes);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching incident summary {CacheKey}", cacheKey);
        }
    }

    public async Task<T?> GetIncidentSummaryAsync<T>(string cacheKey, CancellationToken ct = default)
    {
        try
        {
            return await _resiliencePipeline.ExecuteAsync(async token =>
            {
                var value = await _db.StringGetAsync(cacheKey);

                if (value.HasValue)
                {
                    var data = JsonSerializer.Deserialize<T>(value.ToString());
                    _logger.LogDebug("Incident summary found in cache");
                    return data;
                }

                _logger.LogDebug("Incident summary cache miss");
                return default;
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving incident summary from cache");
            return default;
        }
    }

    public async Task InvalidateIncidentSummaryAsync(string cacheKey, CancellationToken ct = default)
    {
        try
        {
            await _resiliencePipeline.ExecuteAsync(async token =>
            {
                await _db.KeyDeleteAsync(cacheKey);
                _logger.LogInformation("Incident summary {CacheKey} cache invalidated", cacheKey);
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating incident summary {CacheKey} cache", cacheKey);
        }
    }
}
