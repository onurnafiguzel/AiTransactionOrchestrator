using MassTransit;
using Microsoft.EntityFrameworkCore;
using Transaction.Infrastructure.Outbox;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Api.Outbox;

public sealed class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    IPublishEndpoint publishEndpoint,
    ILogger<OutboxPublisherService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();

            var messages = await db.OutboxMessages
                .Where(x => x.PublishedAtUtc == null)
                .OrderBy(x => x.OccurredAtUtc)
                .Take(20)
                .ToListAsync(stoppingToken);

            foreach (var msg in messages)
            {
                var type = Type.GetType(msg.Type);
                if (type is null) continue;

                var payload = System.Text.Json.JsonSerializer.Deserialize(msg.Payload, type);
                if (payload is null) continue;

                await publishEndpoint.Publish(payload, stoppingToken);

                msg.MarkPublished();
            }

            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(1000, stoppingToken);
        }
    }
}
