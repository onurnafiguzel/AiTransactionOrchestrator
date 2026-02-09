using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Merchant'ı Redis-backed blacklist/whitelist'e karşı kontrol et
/// 
/// UserId kullanımı:
/// - User-specific merchant restrictions (kullanıcı belirli satıcılarla işlem yapamaz)
/// - User merchant preferences (tercih edilen satıcılar)
/// - Redis: user:merchant:blacklist:{userId} → SET of merchant IDs
/// - Redis: user:merchant:whitelist:{userId} → SET of trusted merchants
/// </summary>
public sealed class MerchantRiskRule(
    IMerchantRiskCacheService merchantCache,
    ILogger<MerchantRiskRule> logger) : IFraudDetectionRule
{
    public string RuleName => "MerchantRiskAssessment";

    public async Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        // TODO: Check user-specific merchant blacklist
        // var isUserBlacklisted = await _userMerchantCache
        //     .IsBlacklistedForUserAsync(context.UserId, context.MerchantId, ct);
        // if (isUserBlacklisted) return FraudRuleResult(...);

        // Check global merchant blacklist (Redis SET)
        if (await merchantCache.IsBlacklistedAsync(context.MerchantId, ct))
        {
            logger.LogWarning(
                "Blacklisted merchant detected for user {UserId}: {MerchantId}",
                context.UserId, context.MerchantId);
            return new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 95,
                Reason: "Merchant is blacklisted");
        }

        // Check global merchant whitelist (Redis SET)
        if (await merchantCache.IsWhitelistedAsync(context.MerchantId, ct))
        {
            logger.LogDebug(
                "Whitelisted merchant for user {UserId}: {MerchantId}",
                context.UserId, context.MerchantId);
            return new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 5,
                Reason: "Merchant is trusted (whitelisted)");
        }

        // Unknown merchant - moderate risk
        return new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: 30,
            Reason: "Merchant risk profile unknown");
    }
}
