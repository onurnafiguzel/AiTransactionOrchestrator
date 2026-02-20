using Fraud.Worker.Caching;

namespace Fraud.Worker.Rules;

/// <summary>
/// Kullanıcı günlük sınırı kontrolü
/// Her kullanıcının günlük maksimum işlem tutarı vardır.
/// Bugün yapılan toplam işlemler sınırı aşarsa flag et.
/// </summary>
public sealed class UserDailyLimitRule(
    IUserDailySpendingCacheService dailySpendingCache,
    ILogger<UserDailyLimitRule> logger) : IFraudDetectionRule
{
    private const decimal DefaultDailyLimit = 100000; // 100K default

    public string RuleName => "UserDailyLimitCheck";

    public async Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        // Get user's daily limit and current spending from Redis
        var dailyLimit = await dailySpendingCache.GetDailyLimitAsync(context.UserId, ct);
        var currentDailySpent = await dailySpendingCache.GetDailySpentAsync(context.UserId, ct);

        var projectedTotal = currentDailySpent + context.Amount;

        if (projectedTotal > dailyLimit)
        {
            logger.LogWarning(
                "Daily limit exceeded for user {UserId}. Current: {Current}, New: {New}, Limit: {Limit}",
                context.UserId, currentDailySpent, context.Amount, dailyLimit);

            return new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 70,
                Reason: $"Daily limit would be exceeded: {projectedTotal} > {dailyLimit}");
        }

        // Record this spending to daily total
        await dailySpendingCache.AddDailySpendingAsync(context.UserId, context.Amount, ct);

        var utilizationPercent = (projectedTotal / dailyLimit) * 100;
        var riskScore = (int)Math.Min(50, utilizationPercent);

        logger.LogDebug(
            "User {UserId} daily spending: {Spent}/{Limit} ({Percent:F1}%)",
            context.UserId, projectedTotal, dailyLimit, utilizationPercent);

        return new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: riskScore,
            Reason: $"Daily limit check: {utilizationPercent:F1}% of {dailyLimit} utilized");
    }
}
