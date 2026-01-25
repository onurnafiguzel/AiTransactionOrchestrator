using BuildingBlocks.Contracts.Fraud;
using BuildingBlocks.Contracts.Observability;
using Fraud.Worker.AI;
using MassTransit;

namespace Fraud.Worker.Consumers;

public sealed class FraudCheckRequestedConsumer(
    IFraudExplanationGenerator llm,
    FallbackFraudExplanationGenerator fallback,
    ILogger<FraudCheckRequestedConsumer> logger)
    : IConsumer<FraudCheckRequested>
{
    public async Task Consume(ConsumeContext<FraudCheckRequested> context)
    {
        var msg = context.Message;

        var cid =
                context.Headers.Get<string>(Correlation.HeaderName)
                ?? context.CorrelationId?.ToString("N")
                ?? context.Message.CorrelationId
                ?? Guid.NewGuid().ToString("N");

        var riskScore = msg.Amount >= 10000 ? 85 : 15;
        var decision = riskScore >= 70 ? "Reject" : "Approve";

        string explanation;

        try
        {
            explanation = await llm.GenerateAsync(
                    amount: msg.Amount,
                    currency: msg.Currency,
                    riskScore: riskScore,
                    decision: decision,
                    merchantId: msg.MerchantId,
                    correlationId: msg.CorrelationId,
                    ct: context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM failed, using fallback explanation.");
            explanation = await fallback.GenerateAsync(
                msg.Amount,
                msg.Currency,
                riskScore,
                decision,
                msg.MerchantId,
                msg.CorrelationId,
                context.CancellationToken);
        }

        await context.Publish(new FraudCheckCompleted(
            TransactionId: msg.TransactionId,
            RiskScore: riskScore,
            Decision: decision == "Reject" ? FraudDecision.Reject : FraudDecision.Approve,
            Explanation: explanation,
            CorrelationId: msg.CorrelationId
        ), pub =>
        {
            pub.Headers.Set(Correlation
                .HeaderName, cid);
        });

        logger.LogInformation("Fraud hesaplandı.");

    }
}
