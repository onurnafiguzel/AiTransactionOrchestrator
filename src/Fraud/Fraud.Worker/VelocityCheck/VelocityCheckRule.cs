using Fraud.Worker.Rules;

namespace Fraud.Worker.VelocityCheck;

/// <summary>
/// Velocity Check: Aynı kullanıcıdan kısa zaman içinde birçok başarısız işlem
/// N dakika içinde X başarısız işlem → Red Flag
/// </summary>
public class VelocityCheckRule(IVelocityCheckService velocityCheckService) : IFraudDetectionRule
{
    private const int RejectionThreshold = 3;
    private const int RiskScore = 80;

    public string RuleName => "Velocity Check";

    public async Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        var rejectedCount = await velocityCheckService.GetRejectedTransactionCountAsync(
            context.UserId.ToString(), ct);

        if (rejectedCount >= RejectionThreshold)
        {
            return new FraudRuleResult(
                RuleName: "Velocity Check",
                RiskScore: RiskScore,
                IsFraud: true,
                Reason: $"{rejectedCount} başarısız işlem tespit edildi " +
                        $"(Threshold: {RejectionThreshold}) - Hesap potansiyel olarak ele alınıyor"
            );
        }

        return new FraudRuleResult(
            RuleName: "Velocity Check",
            RiskScore: 0,
            IsFraud: false,
            Reason: $"{rejectedCount} başarısız işlem tespit edildi (Threshold: {RejectionThreshold})"
        );
    }
}
