using Fraud.Worker.Policies;
using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Fraud detection engine - tüm rule'ları orchestrate eder.
/// Circuit breaker ile korunmuş - cascade failures prevent eder.
/// Primary constructor pattern (C# 12+).
/// </summary>
public sealed class FraudDetectionEngine(
    IEnumerable<IFraudDetectionRule> rules,
    FraudCheckCircuitBreakerPolicy circuitBreakerPolicy,
    ILogger<FraudDetectionEngine> logger)
{
    public async Task<FraudDetectionResult> AnalyzeAsync(FraudDetectionContext context, CancellationToken ct)
    {
        logger.LogDebug(
            "🔍 Starting fraud analysis for transaction {TransactionId} from merchant {MerchantId}",
            context.TransactionId, context.MerchantId);

        var results = new List<FraudRuleResult>();
        var riskScores = new List<int>();

        // Tüm rule'ları çalıştır circuit breaker protection ile
        foreach (var rule in rules)
        {
            try
            {
                // Circuit breaker ile rule'ları wrap et
                var result = await circuitBreakerPolicy.ExecuteAsync(
                    async (cancelToken) => await rule.EvaluateAsync(context, cancelToken),
                    ct);

                results.Add(result);
                riskScores.Add(result.RiskScore);

                if (result.IsFraud)
                {
                    logger.LogWarning(
                        "⚠️  Fraud detected by rule {RuleName}: {Reason}",
                        result.RuleName, result.Reason);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "❌ Error executing rule {RuleName} for transaction {TransactionId}",
                    rule.RuleName,
                    context.TransactionId);

                // Rule fail olursa high risk score ekle
                riskScores.Add(50);
                results.Add(new FraudRuleResult(
                    RuleName: rule.RuleName,
                    IsFraud: false,
                    RiskScore: 50,
                    Reason: $"Rule failed with error: {ex.Message}"));
            }
        }

        // Risk score'u hesapla (weighted average)
        var overallRiskScore = CalculateOverallRiskScore(riskScores);
        var decision = overallRiskScore >= 70 ? "Reject" : "Approve";

        var detectionResult = new FraudDetectionResult(
            TransactionId: context.TransactionId,
            RiskScore: overallRiskScore,
            Decision: decision,
            RuleResults: results);

        logger.LogInformation(
            "✅ Fraud analysis completed for transaction {TransactionId}. Risk Score: {RiskScore}, Decision: {Decision}",
            context.TransactionId, overallRiskScore, decision);

        return detectionResult;
    }

    private static int CalculateOverallRiskScore(List<int> scores)
    {
        if (scores.Count == 0)
            return 0;

        // Highest score wins (take maximum)
        return scores.Max();

        // Alternatif: Average
        // return (int)Math.Round(scores.Average());

        // Alternatif: Weighted (first rules have higher weight)
        // return (int)Math.Round(scores.Select((s, i) => s * (1 - 0.1 * i)).Average());
    }
}

public sealed record FraudDetectionResult(
    Guid TransactionId,
    int RiskScore,
    string Decision,
    IReadOnlyList<FraudRuleResult> RuleResults);
