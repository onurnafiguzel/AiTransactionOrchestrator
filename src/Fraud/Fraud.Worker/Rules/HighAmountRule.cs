using Fraud.Worker.Caching;

namespace Fraud.Worker.Rules;

/// <summary>
/// Büyük işlem tutarını kontrol et
/// İşlem tutarı belirli eşik değerleri aşarsa flag et.
/// Per-user thresholds'ı Redis'ten alır - premium kullanıcılar daha yüksek limitler
/// </summary>
public sealed class HighAmountRule(
    IUserThresholdCacheService userThresholdCache) : IFraudDetectionRule
{
    private const decimal HighAmountThreshold = 10000;
    private const decimal VeryHighAmountThreshold = 50000;

    public string RuleName => "HighAmountDetection";

    public async Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        // Get user-specific threshold from Redis (premium users have higher limits)
        var userThreshold = await userThresholdCache.GetUserThresholdAsync(context.UserId, ct)
            ?? VeryHighAmountThreshold;

        if (context.Amount >= userThreshold)
        {
            return new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 85,
                Reason: $"Transaction amount {context.Amount} exceeds user threshold {userThreshold}");
        }

        if (context.Amount >= HighAmountThreshold)
        {
            return new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 65,
                Reason: $"High transaction amount: {context.Amount} {context.Currency}");
        }

        return new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: 10,
            Reason: "Amount within normal range");
    }
}
