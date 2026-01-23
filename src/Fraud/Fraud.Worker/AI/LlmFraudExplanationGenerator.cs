namespace Fraud.Worker.AI;

public sealed class LlmFraudExplanationGenerator(
    ILogger<LlmFraudExplanationGenerator> logger)
    : IFraudExplanationGenerator
{
    public async Task<string> GenerateAsync(
        decimal amount,
        string currency,
        int riskScore,
        string decision,
        string merchantId,
        string correlationId,
        CancellationToken ct)
    {
        // Simulated latency
        await Task.Delay(600, ct);

        logger.LogInformation("LLM explanation generated.");

        return decision == "Reject"
            ? $"The transaction was rejected because the risk indicators suggest a high likelihood of fraudulent behavior given the amount ({amount} {currency}) and risk score ({riskScore})."
            : $"The transaction was approved as the assessed risk score ({riskScore}) does not indicate suspicious behavior for the given amount.";
    }
}
