using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using Transaction.Application.Abstractions;

namespace Transaction.Updater.Worker.Consumers;

public sealed class TransactionApprovedConsumer(
    ITransactionRepository repo,
    ILogger<TransactionApprovedConsumer> logger)
    : IConsumer<TransactionApproved>
{
    public async Task Consume(ConsumeContext<TransactionApproved> context)
    {
        var msg = context.Message;

        var tx = await repo.Get(msg.TransactionId, context.CancellationToken);
        if (tx is null)
        {
            logger.LogWarning("Transaction not found for approval. TxId={TransactionId}", msg.TransactionId);
            return;
        }

        tx.MarkApproved(msg.RiskScore, msg.Explanation);
        await repo.Save(tx, context.CancellationToken);

        logger.LogInformation("Transaction approved updated in DB. TxId={TransactionId} CorrelationId={CorrelationId}",
            msg.TransactionId, msg.CorrelationId);
    }
}
