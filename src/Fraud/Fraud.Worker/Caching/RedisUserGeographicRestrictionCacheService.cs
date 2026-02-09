using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Caching;

/// <summary>
/// User başına geographic restrictions - kullanıcı belirli ülkelerde işlem yapamaz
/// </summary>
public interface IUserGeographicRestrictionCacheService
{
    Task<bool> IsRestrictedCountryAsync(Guid userId, string countryCode, CancellationToken ct = default);
    Task AddRestrictedCountryAsync(Guid userId, string countryCode, CancellationToken ct = default);
    Task RemoveRestrictedCountryAsync(Guid userId, string countryCode, CancellationToken ct = default);
    Task<string[]> GetUserTransactionCountriesAsync(Guid userId, CancellationToken ct = default);
    Task AddUserTransactionCountryAsync(Guid userId, string countryCode, CancellationToken ct = default);
}

public sealed class RedisUserGeographicRestrictionCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisUserGeographicRestrictionCacheService> logger) : IUserGeographicRestrictionCacheService
{
    private readonly IDatabase _db = redis.GetDatabase();
    private const string RestrictedKeyPrefix = "user:country:restricted:";
    private const string TransactionKeyPrefix = "user:transaction:countries:";

    public async Task<bool> IsRestrictedCountryAsync(Guid userId, string countryCode, CancellationToken ct = default)
    {
        try
        {
            var key = $"{RestrictedKeyPrefix}{userId}";
            var result = await _db.SetContainsAsync(key, countryCode);

            if (result)
            {
                logger.LogWarning(
                    "Restricted country detected for user {UserId}: {CountryCode}",
                    userId, countryCode);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error checking restricted country from Redis for user {UserId}",
                userId);
            return false; // Fail-safe
        }
    }

    public async Task AddRestrictedCountryAsync(Guid userId, string countryCode, CancellationToken ct = default)
    {
        try
        {
            var key = $"{RestrictedKeyPrefix}{userId}";
            await _db.SetAddAsync(key, countryCode);
            logger.LogInformation(
                "Restricted country added for user {UserId}: {CountryCode}",
                userId, countryCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding restricted country to Redis for user {UserId}", userId);
        }
    }

    public async Task RemoveRestrictedCountryAsync(Guid userId, string countryCode, CancellationToken ct = default)
    {
        try
        {
            var key = $"{RestrictedKeyPrefix}{userId}";
            await _db.SetRemoveAsync(key, countryCode);
            logger.LogInformation(
                "Restricted country removed for user {UserId}: {CountryCode}",
                userId, countryCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing restricted country from Redis for user {UserId}", userId);
        }
    }

    public async Task<string[]> GetUserTransactionCountriesAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var key = $"{TransactionKeyPrefix}{userId}";
            var countries = await _db.SetMembersAsync(key);

            return countries.Length == 0
                ? Array.Empty<string>()
                : countries.Select(c => c.ToString()).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error getting user transaction countries from Redis for user {UserId}",
                userId);
            return Array.Empty<string>();
        }
    }

    public async Task AddUserTransactionCountryAsync(Guid userId, string countryCode, CancellationToken ct = default)
    {
        try
        {
            var key = $"{TransactionKeyPrefix}{userId}";
            await _db.SetAddAsync(key, countryCode);
            // Set expiration for 90 days
            await _db.KeyExpireAsync(key, TimeSpan.FromDays(90));
            logger.LogDebug(
                "User transaction country added for user {UserId}: {CountryCode}",
                userId, countryCode);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error adding user transaction country to Redis for user {UserId}",
                userId);
        }
    }
}
