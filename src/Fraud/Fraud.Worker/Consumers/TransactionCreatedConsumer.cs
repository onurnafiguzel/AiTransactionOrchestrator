using BuildingBlocks.Contracts.Transactions;
using MassTransit;

namespace Fraud.Worker.Consumers;

public sealed class TransactionCreatedConsumer(ILogger<TransactionCreatedConsumer> logger)
    : IConsumer<TransactionCreated>
{
    public Task Consume(ConsumeContext<TransactionCreated> context)
    {
        var msg = context.Message;

        logger.LogInformation(
            "Fraud.Worker received TransactionCreated. TransactionId={TransactionId} Amount={Amount} CorrelationId={CorrelationId}",
            msg.TransactionId, msg.Amount, msg.CorrelationId);

        return Task.CompletedTask;
    }
}
