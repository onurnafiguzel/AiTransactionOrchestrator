using StackExchange.Redis;

namespace Fraud.Worker.Rules;

/// <summary>
/// Redis-backed Geographic Risk Scores Service
/// Uses Redis HASH data structure for country → risk score mapping
/// </summary>
public interface IGeographicRiskCacheService
{
    Task<int?> GetCountryRiskScoreAsync(string countryCode, CancellationToken ct = default);
    Task SetCountryRiskScoreAsync(string countryCode, int riskScore, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetAllRiskScoresAsync(CancellationToken ct = default);
    Task SeedDefaultDataAsync(CancellationToken ct = default);
}

public sealed class RedisGeographicRiskCacheService : IGeographicRiskCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisGeographicRiskCacheService> _logger;

    private const string RiskScoresKey = "geo:risk:scores";

    public RedisGeographicRiskCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisGeographicRiskCacheService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<int?> GetCountryRiskScoreAsync(string countryCode, CancellationToken ct = default)
    {
        try
        {
            var value = await _db.HashGetAsync(RiskScoresKey, countryCode.ToUpperInvariant());

            if (value.HasValue && int.TryParse(value.ToString(), out var score))
            {
                _logger.LogDebug("Geographic risk score for {Country}: {Score}", countryCode, score);
                return score;
            }

            return null; // Country not in cache
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting geographic risk score for {Country}", countryCode);
            return null;
        }
    }

    public async Task SetCountryRiskScoreAsync(string countryCode, int riskScore, CancellationToken ct = default)
    {
        try
        {
            await _db.HashSetAsync(RiskScoresKey, countryCode.ToUpperInvariant(), riskScore);
            _logger.LogInformation("Set geographic risk score for {Country}: {Score}", countryCode, riskScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting geographic risk score for {Country}", countryCode);
        }
    }

    public async Task<Dictionary<string, int>> GetAllRiskScoresAsync(CancellationToken ct = default)
    {
        try
        {
            var entries = await _db.HashGetAllAsync(RiskScoresKey);
            var result = new Dictionary<string, int>();

            foreach (var entry in entries)
            {
                if (int.TryParse(entry.Value.ToString(), out var score))
                {
                    result[entry.Name.ToString()] = score;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all geographic risk scores");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Seed default country risk scores to Redis
    /// </summary>
    public async Task SeedDefaultDataAsync(CancellationToken ct = default)
    {
        try
        {
            // Check if already seeded
            var count = await _db.HashLengthAsync(RiskScoresKey);
            if (count > 0)
            {
                _logger.LogDebug("Geographic risk data already seeded. Count: {Count}", count);
                return;
            }

            // Default risk scores by country
            var riskScores = new Dictionary<string, int>
            {
                // High Risk (80-95)
                { "KP", 95 }, // North Korea
                { "IR", 90 }, // Iran
                { "SY", 90 }, // Syria
                { "CU", 85 }, // Cuba
                { "VE", 80 }, // Venezuela
                
                // Moderate Risk (40-60)
                { "RU", 60 }, // Russia
                { "BY", 55 }, // Belarus
                { "KZ", 50 }, // Kazakhstan
                { "UZ", 50 }, // Uzbekistan
                { "NG", 55 }, // Nigeria
                { "PK", 45 }, // Pakistan
                
                // Low Risk (10-30)
                { "US", 15 }, // United States
                { "GB", 10 }, // United Kingdom
                { "DE", 10 }, // Germany
                { "FR", 10 }, // France
                { "NL", 10 }, // Netherlands
                { "CH", 5 },  // Switzerland
                { "SE", 5 },  // Sweden
                { "NO", 5 },  // Norway
                { "DK", 5 },  // Denmark
                { "FI", 5 },  // Finland
                { "TR", 1 }, // Turkey
            };

            foreach (var (country, score) in riskScores)
            {
                await _db.HashSetAsync(RiskScoresKey, country, score);
            }

            _logger.LogInformation("Seeded default geographic risk scores. Count: {Count}", riskScores.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding default geographic risk data");
        }
    }
}
