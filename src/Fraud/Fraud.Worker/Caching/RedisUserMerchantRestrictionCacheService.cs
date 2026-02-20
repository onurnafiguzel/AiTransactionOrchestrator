using StackExchange.Redis;

namespace Fraud.Worker.Caching;

/// <summary>
/// User başına merchant restrictions - kullanıcı belirli satıcılarla işlem yapamaz
/// </summary>
public interface IUserMerchantRestrictionCacheService
{
    Task<bool> IsRestrictedMerchantAsync(Guid userId, string merchantId, CancellationToken ct = default);
    Task AddRestrictedMerchantAsync(Guid userId, string merchantId, CancellationToken ct = default);
    Task RemoveRestrictedMerchantAsync(Guid userId, string merchantId, CancellationToken ct = default);
}

public sealed class RedisUserMerchantRestrictionCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisUserMerchantRestrictionCacheService> logger) : IUserMerchantRestrictionCacheService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string KeyPrefix = "user:merchant:restricted:";

    public async Task<bool> IsRestrictedMerchantAsync(Guid userId, string merchantId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{userId}";
            var result = await _db.SetContainsAsync(key, merchantId);

            if (result)
            {
                logger.LogWarning(
                    "Restricted merchant detected for user {UserId}: {MerchantId}",
                    userId, merchantId);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error checking restricted merchant from Redis for user {UserId}",
                userId);
            return false; // Fail-safe: don't block transaction
        }
    }

    public async Task AddRestrictedMerchantAsync(Guid userId, string merchantId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{userId}";
            await _db.SetAddAsync(key, merchantId);
            logger.LogInformation(
                "Restricted merchant added for user {UserId}: {MerchantId}",
                userId, merchantId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error adding restricted merchant to Redis for user {UserId}",
                userId);
        }
    }

    public async Task RemoveRestrictedMerchantAsync(Guid userId, string merchantId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{KeyPrefix}{userId}";
            await _db.SetRemoveAsync(key, merchantId);
            logger.LogInformation(
                "Restricted merchant removed for user {UserId}: {MerchantId}",
                userId, merchantId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error removing restricted merchant from Redis for user {UserId}",
                userId);
        }
    }
}
