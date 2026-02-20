namespace Fraud.Worker.VelocityCheck;

/// <summary>
/// Her 1 saatte bir velocity check history'den eski kayıtları temizle
/// </summary>
public class VelocityCheckCleanupHostedService(
    IVelocityCheckService velocityCheckService,
    ILogger<VelocityCheckCleanupHostedService> logger)
    : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly int _ageInMinutes = 1440; // 24 saat

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("VelocityCheckCleanupHostedService started. Cleanup interval: {IntervalMinutes} minutes",
            _cleanupInterval.TotalMinutes);

        // İlk çalışmadan önce biraz bekle
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogDebug("Starting velocity check cleanup...");

                await velocityCheckService.CleanupOldRecordsAsync(_ageInMinutes);

                logger.LogInformation("Velocity check cleanup completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during velocity check cleanup");
            }

            // Sonraki cleanup'a kadar bekle
            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("VelocityCheckCleanupHostedService stopped");
        await base.StopAsync(cancellationToken);
    }
}
