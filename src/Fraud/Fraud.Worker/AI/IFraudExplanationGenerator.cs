namespace Fraud.Worker.AI;

public interface IFraudExplanationGenerator
{
    Task<string> GenerateAsync(
       decimal amount,
       string currency,
       int riskScore,
       string decision,
       string merchantId,
       string correlationId,
       CancellationToken ct);
}
