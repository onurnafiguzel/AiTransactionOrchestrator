using Fraud.Worker.Rules;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Caching;

/// <summary>
/// Application startup'ta Redis'e default fraud rule data'larını seed eder
/// </summary>
public sealed class RedisCacheSeederHostedService : IHostedService
{
    private readonly IMerchantRiskCacheService _merchantCache;
    private readonly IGeographicRiskCacheService _geoCache;
    private readonly ILogger<RedisCacheSeederHostedService> _logger;

    public RedisCacheSeederHostedService(
        IMerchantRiskCacheService merchantCache,
        IGeographicRiskCacheService geoCache,
        ILogger<RedisCacheSeederHostedService> logger)
    {
        _merchantCache = merchantCache;
        _geoCache = geoCache;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Redis cache seeder...");

        try
        {
            // Seed merchant blacklist/whitelist
            await _merchantCache.SeedDefaultDataAsync(cancellationToken);

            // Seed geographic risk scores
            await _geoCache.SeedDefaultDataAsync(cancellationToken);

            _logger.LogInformation("Redis cache seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding Redis cache data");
            // Don't throw - allow application to continue
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Redis cache seeder stopped");
        return Task.CompletedTask;
    }
}
