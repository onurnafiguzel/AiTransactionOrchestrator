using BuildingBlocks.Contracts.Fraud;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Consumers;

public sealed class FraudCheckRequestedConsumer(ILogger<FraudCheckRequestedConsumer> logger)
    : IConsumer<FraudCheckRequested>
{
    public Task Consume(ConsumeContext<FraudCheckRequested> context)
    {
        var msg = context.Message;

        // Basit kural: amount >= 10000 => reject
        var riskScore = msg.Amount >= 10000 ? 85 : 15;
        var decision = msg.Amount >= 10000 ? FraudDecision.Reject : FraudDecision.Approve;

        var explanation = decision == FraudDecision.Reject
            ? "High amount threshold triggered (>= 10000)."
            : "Amount under threshold; low risk by baseline rule.";

        logger.LogInformation(
            "FraudCheckRequested received. TxId={TransactionId} Amount={Amount} Decision={Decision} CorrelationId={CorrelationId}",
            msg.TransactionId, msg.Amount, decision, msg.CorrelationId);

        return context.Publish(new FraudCheckCompleted(
            TransactionId: msg.TransactionId,
            RiskScore: riskScore,
            Decision: decision,
            Explanation: explanation,
            CorrelationId: msg.CorrelationId
        ));
    }
}
