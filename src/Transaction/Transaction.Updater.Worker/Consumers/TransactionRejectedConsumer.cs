using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Transaction.Application.Abstractions;

namespace Transaction.Updater.Worker.Consumers;

public sealed class TransactionRejectedConsumer(
    ITransactionRepository repo,
    ILogger<TransactionRejectedConsumer> logger)
    : IConsumer<TransactionRejected>
{
    public async Task Consume(ConsumeContext<TransactionRejected> context)
    {
        var msg = context.Message;

        var tx = await repo.Get(msg.TransactionId, context.CancellationToken);
        if (tx is null)
        {
            logger.LogWarning("Transaction not found for rejection. TxId={TransactionId}", msg.TransactionId);
            return;
        }

        tx.MarkRejected(msg.RiskScore, msg.Reason, msg.Explanation);
        await repo.Save(tx, context.CancellationToken);

        logger.LogInformation("Transaction rejected updated in DB. TxId={TransactionId} Reason={Reason} CorrelationId={CorrelationId}",
            msg.TransactionId, msg.Reason, msg.CorrelationId);
    }
}
