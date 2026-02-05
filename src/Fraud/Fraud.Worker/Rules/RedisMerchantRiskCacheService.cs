using StackExchange.Redis;
using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Redis-backed Merchant Blacklist/Whitelist Service
/// Uses Redis SET data structure for O(1) lookup
/// </summary>
public interface IMerchantRiskCacheService
{
    Task<bool> IsBlacklistedAsync(string merchantId, CancellationToken ct = default);
    Task<bool> IsWhitelistedAsync(string merchantId, CancellationToken ct = default);
    Task AddToBlacklistAsync(string merchantId, CancellationToken ct = default);
    Task AddToWhitelistAsync(string merchantId, CancellationToken ct = default);
    Task RemoveFromBlacklistAsync(string merchantId, CancellationToken ct = default);
    Task RemoveFromWhitelistAsync(string merchantId, CancellationToken ct = default);
    Task SeedDefaultDataAsync(CancellationToken ct = default);
}

public sealed class RedisMerchantRiskCacheService : IMerchantRiskCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisMerchantRiskCacheService> _logger;
    
    private const string BlacklistKey = "merchant:blacklist";
    private const string WhitelistKey = "merchant:whitelist";

    public RedisMerchantRiskCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisMerchantRiskCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<bool> IsBlacklistedAsync(string merchantId, CancellationToken ct = default)
    {
        try
        {
            var result = await _db.SetContainsAsync(BlacklistKey, merchantId);
            if (result)
            {
                _logger.LogWarning("Merchant {MerchantId} found in blacklist", merchantId);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking blacklist for merchant {MerchantId}", merchantId);
            return false; // Fail-safe: don't block transaction
        }
    }

    public async Task<bool> IsWhitelistedAsync(string merchantId, CancellationToken ct = default)
    {
        try
        {
            var result = await _db.SetContainsAsync(WhitelistKey, merchantId);
            if (result)
            {
                _logger.LogDebug("Merchant {MerchantId} found in whitelist", merchantId);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking whitelist for merchant {MerchantId}", merchantId);
            return false;
        }
    }

    public async Task AddToBlacklistAsync(string merchantId, CancellationToken ct = default)
    {
        try
        {
            await _db.SetAddAsync(BlacklistKey, merchantId);
            _logger.LogInformation("Merchant {MerchantId} added to blacklist", merchantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding merchant {MerchantId} to blacklist", merchantId);
        }
    }

    public async Task AddToWhitelistAsync(string merchantId, CancellationToken ct = default)
    {
        try
        {
            await _db.SetAddAsync(WhitelistKey, merchantId);
            _logger.LogInformation("Merchant {MerchantId} added to whitelist", merchantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding merchant {MerchantId} to whitelist", merchantId);
        }
    }

    public async Task RemoveFromBlacklistAsync(string merchantId, CancellationToken ct = default)
    {
        try
        {
            await _db.SetRemoveAsync(BlacklistKey, merchantId);
            _logger.LogInformation("Merchant {MerchantId} removed from blacklist", merchantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing merchant {MerchantId} from blacklist", merchantId);
        }
    }

    public async Task RemoveFromWhitelistAsync(string merchantId, CancellationToken ct = default)
    {
        try
        {
            await _db.SetRemoveAsync(WhitelistKey, merchantId);
            _logger.LogInformation("Merchant {MerchantId} removed from whitelist", merchantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing merchant {MerchantId} from whitelist", merchantId);
        }
    }

    /// <summary>
    /// Seed default blacklist/whitelist data to Redis
    /// </summary>
    public async Task SeedDefaultDataAsync(CancellationToken ct = default)
    {
        try
        {
            // Check if already seeded
            var blacklistCount = await _db.SetLengthAsync(BlacklistKey);
            var whitelistCount = await _db.SetLengthAsync(WhitelistKey);

            if (blacklistCount > 0 || whitelistCount > 0)
            {
                _logger.LogDebug("Merchant risk data already seeded. Blacklist: {Blacklist}, Whitelist: {Whitelist}",
                    blacklistCount, whitelistCount);
                return;
            }

            // Seed default blacklist
            var defaultBlacklist = new[]
            {
                "MERCHANT_SUSPICIOUS_001",
                "MERCHANT_BANNED_002",
                "FRAUD_MERCHANT_001",
                "SCAM_SHOP_001"
            };

            foreach (var merchant in defaultBlacklist)
            {
                await _db.SetAddAsync(BlacklistKey, merchant);
            }

            // Seed default whitelist
            var defaultWhitelist = new[]
            {
                "MERCHANT_VERIFIED_001",
                "MERCHANT_VERIFIED_002",
                "AMAZON_TR",
                "TRENDYOL",
                "HEPSIBURADA"
            };

            foreach (var merchant in defaultWhitelist)
            {
                await _db.SetAddAsync(WhitelistKey, merchant);
            }

            _logger.LogInformation("Seeded default merchant risk data. Blacklist: {Blacklist}, Whitelist: {Whitelist}",
                defaultBlacklist.Length, defaultWhitelist.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default merchant risk data");
        }
    }
}
