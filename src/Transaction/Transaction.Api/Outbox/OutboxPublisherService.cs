using MassTransit;
using Microsoft.EntityFrameworkCore;
using Transaction.Infrastructure.Outbox;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Api.Outbox;

public sealed class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisherService> logger)
    : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var instanceId = $"api-{Environment.MachineName}-{Environment.ProcessId}";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
                var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                var store = new OutboxStore(db);

                var claimed = await store.ClaimBatchAsync(
                    batchSize: BatchSize,
                    lockedBy: instanceId,
                    lockFor: TimeSpan.FromSeconds(30),
                    ct: stoppingToken);

                if (claimed.Count == 0)
                {
                    await Task.Delay(750, stoppingToken);
                    continue;
                }

                foreach (var row in claimed)
                {
                    await PublishOne(db, publish, row.Id, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxPublisher loop failed");
                await Task.Delay(1500, stoppingToken);
            }
        }
    }

    private async Task PublishOne(TransactionDbContext db, IPublishEndpoint publish, Guid outboxId, CancellationToken ct)
    {
        var msg = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == outboxId, ct);
        if (msg is null) return;

        try
        {
            var type = Type.GetType(msg.Type);
            if (type is null)
                throw new InvalidOperationException($"Cannot resolve message type: {msg.Type}");

            var payload = System.Text.Json.JsonSerializer.Deserialize(msg.Payload, type);
            if (payload is null)
                throw new InvalidOperationException("Cannot deserialize outbox payload.");

            await publish.Publish(payload, type, ctx =>
            {
                if (!string.IsNullOrWhiteSpace(msg.CorrelationId))
                    ctx.Headers.Set(BuildingBlocks.Contracts.Observability.Correlation.HeaderName, msg.CorrelationId);

                if (!string.IsNullOrWhiteSpace(msg.CorrelationId))
                    ctx.Headers.Set("correlation_id", msg.CorrelationId);
            }, ct);

            msg.MarkPublished();
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Outbox published. OutboxId={OutboxId}",
                msg.Id);

        }
        catch (Exception ex)
        {
            var next = ComputeNextAttemptUtc(msg.AttemptCount + 1);
            msg.MarkFailed(ex.ToString(), next, MaxAttempts);

            await db.SaveChangesAsync(ct);

            logger.LogWarning(ex,
                "Outbox publish failed. OutboxId={OutboxId} Attempt={Attempt} NextAttemptAtUtc={NextAttemptAtUtc}",
                msg.Id, msg.AttemptCount, msg.NextAttemptAtUtc);
        }
    }

    private static DateTime ComputeNextAttemptUtc(int attempt)
    {
        // Exponential backoff with cap + small jitter
        var baseSeconds = Math.Min(60 * 10, (int)Math.Pow(2, Math.Min(10, attempt))); // cap
        var jitter = Random.Shared.Next(0, 500); // ms
        return DateTime.UtcNow.AddSeconds(baseSeconds).AddMilliseconds(jitter);
    }
}
