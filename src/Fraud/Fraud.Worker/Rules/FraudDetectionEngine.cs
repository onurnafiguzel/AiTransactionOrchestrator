using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Fraud detection engine - tüm rule'ları orchestrate eder
/// </summary>
public sealed class FraudDetectionEngine
{
    private readonly IEnumerable<IFraudDetectionRule> _rules;
    private readonly ILogger<FraudDetectionEngine> _logger;

    public FraudDetectionEngine(IEnumerable<IFraudDetectionRule> rules, ILogger<FraudDetectionEngine> logger)
    {
        _rules = rules;
        _logger = logger;
    }

    public async Task<FraudDetectionResult> AnalyzeAsync(FraudDetectionContext context, CancellationToken ct)
    {
        _logger.LogDebug("Starting fraud analysis for transaction {TransactionId} from merchant {MerchantId}",
            context.TransactionId, context.MerchantId);

        var results = new List<FraudRuleResult>();
        var riskScores = new List<int>();

        // Tüm rule'ları çalıştır
        foreach (var rule in _rules)
        {
            try
            {
                var result = await rule.EvaluateAsync(context, ct);
                results.Add(result);
                riskScores.Add(result.RiskScore);

                if (result.IsFraud)
                {
                    _logger.LogWarning("Fraud detected by rule {RuleName}: {Reason}",
                        rule.RuleName, result.Reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing rule {RuleName}", rule.RuleName);
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

        _logger.LogInformation(
            "Fraud analysis completed for transaction {TransactionId}. Risk Score: {RiskScore}, Decision: {Decision}",
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
