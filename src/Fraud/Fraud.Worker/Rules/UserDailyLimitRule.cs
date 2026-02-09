using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Kullanıcı günlük sınırı kontrolü
/// Her kullanıcının günlük maksimum işlem tutarı vardır.
/// Bugün yapılan toplam işlemler sınırı aşarsa flag et.
/// </summary>
public sealed class UserDailyLimitRule(
    ILogger<UserDailyLimitRule> logger) : IFraudDetectionRule
{
    private const decimal DefaultDailyLimit = 100000; // 100K default

    public string RuleName => "UserDailyLimitCheck";

    public Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        // TODO: Redis'ten kullanıcının günlük harcama toplamını al
        // Redis key: daily:spent:{userId}:{date}
        // Redis key: daily:limit:{userId}
        
        // Şimdilik: Başlangıç implementasyonu
        var dailyLimit = DefaultDailyLimit;
        var currentDailySpent = 0m; // TODO: Redis'ten al

        var projectedTotal = currentDailySpent + context.Amount;

        if (projectedTotal > dailyLimit)
        {
            logger.LogWarning(
                "Daily limit exceeded for user {UserId}. Current: {Current}, New: {New}, Limit: {Limit}",
                context.UserId, currentDailySpent, context.Amount, dailyLimit);

            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 70,
                Reason: $"Daily limit would be exceeded: {projectedTotal} > {dailyLimit}"));
        }

        var utilizationPercent = (projectedTotal / dailyLimit) * 100;
        var riskScore = (int)Math.Min(50, utilizationPercent);

        logger.LogDebug(
            "User {UserId} daily spending: {Spent}/{Limit} ({Percent:F1}%)",
            context.UserId, projectedTotal, dailyLimit, utilizationPercent);

        return Task.FromResult(new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: riskScore,
            Reason: $"Daily limit check: {utilizationPercent:F1}% of {dailyLimit} utilized"));
    }
}
