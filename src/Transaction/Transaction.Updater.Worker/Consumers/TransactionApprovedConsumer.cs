using BuildingBlocks.Contracts.Observability;
using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using Transaction.Application.Abstractions;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Persistence;
using Transaction.Updater.Worker.Timeline;

namespace Transaction.Updater.Worker.Consumers;

public sealed class TransactionApprovedConsumer(
    ITransactionRepository repo,
    TransactionDbContext db,
    InboxGuard guard,
    TimelineWriter timeline,
    IUnitOfWork uow,
    ILogger<TransactionApprovedConsumer> logger)
    : IConsumer<TransactionApproved>
{
    public async Task Consume(ConsumeContext<TransactionApproved> context)
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
                logger.LogInformation("Duplicate message ignored. MessageId={MessageId}", messageId);
                return;
            }

            var tx = await repo.Get(context.Message.TransactionId, context.CancellationToken);
            if (tx is null)
            {
                logger.LogWarning("Transaction not found. TxId={TransactionId}", context.Message.TransactionId);
                return;
            }

            tx.MarkApproved(context.Message.RiskScore, context.Message.Explanation);
            await repo.Save(tx, context.CancellationToken);

            await timeline.Append(
                    transactionId: context.Message.TransactionId,
                    eventType: "TransactionApproved",
                    detailsJson: $"{{\"riskScore\":{context.Message.RiskScore}}}",
                    correlationId: context.Message.CorrelationId,
                    source: "transaction-updater",
                    ct: context.CancellationToken);

            await uow.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation("Transaction approved updated in DB.");
        }

    }
}
