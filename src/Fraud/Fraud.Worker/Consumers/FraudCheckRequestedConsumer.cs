using BuildingBlocks.Contracts.Fraud;
using BuildingBlocks.Contracts.Observability;
using Fraud.Worker.AI;
using Fraud.Worker.Rules;
using Fraud.Worker.VelocityCheck;
using MassTransit;

namespace Fraud.Worker.Consumers;

public sealed class FraudCheckRequestedConsumer(
    FraudDetectionEngine fraudEngine,
    IFraudExplanationGenerator llm,
    FallbackFraudExplanationGenerator fallback,
    IVelocityCheckService velocityCheckService,
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

        // Advanced fraud detection - tüm rule'ları çalıştır
        var fraudContext = new FraudDetectionContext(
            TransactionId: msg.TransactionId,
            MerchantId: msg.MerchantId,
            Amount: msg.Amount,
            Currency: msg.Currency,
            CustomerIp: "0.0.0.0", // RabbitMQ message'tan gelmiyor, enhanceable
            CustomerCountry: null, // Gerçek implementasyonda IP geolocation'dan al
            TransactionTime: DateTime.UtcNow);

        var fraudAnalysis = await fraudEngine.AnalyzeAsync(fraudContext, context.CancellationToken);

        var riskScore = fraudAnalysis.RiskScore;
        var decision = fraudAnalysis.Decision;

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

        // Eğer red flag aldıysa, velocity check history'sine kaydet
        if (decision == "Reject")
        {
            await velocityCheckService.RecordRejectedTransactionAsync(
                userId: msg.MerchantId, // TODO: Gerçek User ID kullan
                amount: msg.Amount,
                merchant: msg.MerchantId,
                country: fraudContext.CustomerCountry ?? "Unknown");
                
            logger.LogWarning("Transaction rejected - recorded for velocity check. UserId: {UserId}",
                msg.MerchantId);
        }

        await context.Publish(new FraudCheckCompleted(
            TransactionId: msg.TransactionId,
            RiskScore: riskScore,
            Decision: decision == "Reject" ? FraudDecision.Reject : FraudDecision.Approve,
            Explanation: explanation,
            CorrelationId: msg.CorrelationId
        ), pub =>
        {
            pub.Headers.Set(Correlation.HeaderName, cid);
        });

        logger.LogInformation("Fraud analysis completed. Decision: {Decision}, Risk Score: {RiskScore}",
            decision, riskScore);
    }
}
