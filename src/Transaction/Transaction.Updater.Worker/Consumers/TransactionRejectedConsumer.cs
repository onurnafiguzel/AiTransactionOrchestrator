using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using Transaction.Application.Abstractions;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Updater.Worker.Consumers;

public sealed class TransactionRejectedConsumer(
    ITransactionRepository repo,
    TransactionDbContext db,
    InboxGuard guard,
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
        await uow.SaveChangesAsync(context.CancellationToken);
    }
}