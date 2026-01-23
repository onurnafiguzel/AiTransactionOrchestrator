namespace Fraud.Worker.AI;

public sealed class FallbackFraudExplanationGenerator
    : IFraudExplanationGenerator
{
    public Task<string> GenerateAsync(
           decimal amount,
           string currency,
           int riskScore,
           string decision,
           string merchantId,
           string correlationId,
           CancellationToken ct)
    {
        var text =
            decision == "Reject"
                ? $"Rejected due to elevated risk score ({riskScore}). Amount {amount} {currency} triggered configured thresholds."
                : $"Approved. Risk score ({riskScore}) is within acceptable limits for amount {amount} {currency}.";

        return Task.FromResult(text);
    }
}
