using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Merchant'ı Redis-backed blacklist/whitelist'e karşı kontrol et
/// </summary>
public sealed class MerchantRiskRule : IFraudDetectionRule
{
    private readonly IMerchantRiskCacheService _merchantCache;
    private readonly ILogger<MerchantRiskRule> _logger;

    public string RuleName => "MerchantRiskAssessment";

    public MerchantRiskRule(
        IMerchantRiskCacheService merchantCache,
        ILogger<MerchantRiskRule> logger)
    {
        _merchantCache = merchantCache;
        _logger = logger;
    }

    public async Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        // Check blacklist (Redis SET)
        if (await _merchantCache.IsBlacklistedAsync(context.MerchantId, ct))
        {
            _logger.LogWarning("Blacklisted merchant detected: {MerchantId}", context.MerchantId);
            return new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 95,
                Reason: "Merchant is blacklisted");
        }

        // Check whitelist (Redis SET)
        if (await _merchantCache.IsWhitelistedAsync(context.MerchantId, ct))
        {
            _logger.LogDebug("Whitelisted merchant: {MerchantId}", context.MerchantId);
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
