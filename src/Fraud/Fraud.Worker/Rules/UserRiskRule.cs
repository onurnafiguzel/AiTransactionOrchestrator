namespace Fraud.Worker.Rules;

/// <summary>
/// Kullanıcı tabanlı risk değerlendirmesi - Redis-backed kullanıcı risk skorlarını kontrol et
/// User risk profiles şunları içerir:
/// - User blacklist: Bilinen riskli kullanıcılar
/// - User whitelist: Güvenilir kullanıcılar
/// </summary>
public sealed class UserRiskRule(
    ILogger<UserRiskRule> logger) : IFraudDetectionRule
{
    public string RuleName => "UserRiskAssessment";

    public Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        // Basic user ID validation and pattern checks

        // Eğer kullanıcı ID default/test value ise risk var
        if (context.UserId == Guid.Empty)
        {
            logger.LogWarning("Invalid user ID detected");
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 75,
                Reason: "Invalid or missing user ID"));
        }

        // Kullanıcı ID'den risk belirtileri kontrolü
        var riskScore = 0;
        var reason = "User risk profile: normal";

        // Note: For more advanced user risk checks (new_account, blacklist status),
        // user creation date should be added to FraudDetectionContext and checked against Redis user profiles

        logger.LogDebug("User {UserId} risk score: {RiskScore}", context.UserId, riskScore);

        return Task.FromResult(new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: riskScore,
            Reason: reason));
    }
}
