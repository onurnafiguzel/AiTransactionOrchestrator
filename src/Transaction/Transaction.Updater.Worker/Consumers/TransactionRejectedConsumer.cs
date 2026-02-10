using BuildingBlocks.Contracts.Observability;
using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using Transaction.Application.Abstractions;
using Transaction.Infrastructure.Caching;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Persistence;
using Transaction.Updater.Worker.Timeline;

namespace Transaction.Updater.Worker.Consumers;

public sealed class TransactionRejectedConsumer(
    ITransactionRepository repo,
    TransactionDbContext db,
    InboxGuard guard,
    TimelineWriter timeline,
    IUnitOfWork uow,
    ITransactionCacheService cacheService,
    ILogger<TransactionRejectedConsumer> logger)
    : IConsumer<TransactionRejected>
{
    public async Task Consume(ConsumeContext<TransactionRejected> context)
    {
        var messageId = context.MessageId ?? context.CorrelationId ?? NewId.NextGuid();

        var cid =
            context.Headers.Get<string>(Correlation.HeaderName)
            ?? context.CorrelationId?.ToString("N")
            ?? context.Message.CorrelationId
            ?? Guid.NewGuid().ToString("N");

        CorrelationContext.CorrelationId = cid;

        using (Serilog.Context.LogContext.PushProperty("message_id", messageId))
        using (Serilog.Context.LogContext.PushProperty("transaction_id", context.Message.TransactionId))
        {
            if (!await guard.TryBeginAsync(messageId, context.CancellationToken))
            {
                logger.LogInformation("Duplicate message ignored.");
                return;
            }

            var tx = await repo.Get(context.Message.TransactionId, context.CancellationToken);
            if (tx is null)
            {
                logger.LogWarning("Transaction not found.");
                return;
            }

            tx.MarkRejected(context.Message.RiskScore, context.Message.Reason, context.Message.Explanation);
            await repo.Save(tx, context.CancellationToken);

            await timeline.Append(
                context.Message.TransactionId,
                "TransactionRejected",
                $"{{\"riskScore\":{context.Message.RiskScore},\"reason\":\"{context.Message.Reason}\"}}",
                cid,
                "transaction-updater",
                context.CancellationToken);

            await uow.SaveChangesAsync(context.CancellationToken);

            // Cache invalidation
            await cacheService.InvalidateTransactionAsync(context.Message.TransactionId, context.CancellationToken);

            logger.LogInformation("Transaction rejected updated in DB. Reason={Reason}", context.Message.Reason);
        }
    }
}
