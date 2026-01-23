using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using System;
using Transaction.Application.Abstractions;
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
    ILogger<TransactionRejectedConsumer> logger)
    : IConsumer<TransactionRejected>
{
    public async Task Consume(ConsumeContext<TransactionRejected> context)
    {
        Guid messageId = context.MessageId
                         ?? context.CorrelationId
                         ?? Guid.NewGuid();

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

        tx.MarkRejected(context.Message.RiskScore, context.Message.Reason, context.Message.Explanation);
        await repo.Save(tx, context.CancellationToken);

        await timeline.Append(
                    context.Message.TransactionId,
                    "TransactionRejected",
                    $"{{\"riskScore\":{context.Message.RiskScore},\"reason\":\"{context.Message.Reason}\"}}",
                    context.Message.CorrelationId,
                    "transaction-updater",
                    context.CancellationToken);

        await uow.SaveChangesAsync(context.CancellationToken);
    }
}