namespace Fraud.Worker.Rules;

/// <summary>
/// Büyük işlem tutarını kontrol et
/// İşlem tutarı belirli eşik değerleri aşarsa flag et.
/// 
/// UserId kullanımı:
/// - Per-user amount thresholds (premium kullanıcılar daha yüksek limitler)
/// - User daily/monthly spending patterns
/// - Redis: PUT user:threshold:{userId} → amount
/// </summary>
public sealed class HighAmountRule : IFraudDetectionRule
{
    private const decimal HighAmountThreshold = 10000;
    private const decimal VeryHighAmountThreshold = 50000;

    public string RuleName => "HighAmountDetection";

    public Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        // TODO: Redis'ten user-specific thresholds'ı al
        // var userThreshold = await _userThresholdCache.GetAsync(context.UserId, ct) 
        //     ?? VeryHighAmountThreshold;

        if (context.Amount >= VeryHighAmountThreshold)
        {
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 85,
                Reason: $"Very high transaction amount: {context.Amount} {context.Currency}"));
        }

        if (context.Amount >= HighAmountThreshold)
        {
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 65,
                Reason: $"High transaction amount: {context.Amount} {context.Currency}"));
        }

        return Task.FromResult(new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: 10,
            Reason: "Amount within normal range"));
    }
}
