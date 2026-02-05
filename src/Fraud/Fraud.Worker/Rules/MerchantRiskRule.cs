using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Merchant'ı blacklist/whitelist'e karşı kontrol et
/// </summary>
public sealed class MerchantRiskRule : IFraudDetectionRule
{
    private readonly ILogger<MerchantRiskRule> _logger;
    
    // Whitelist: düşük risk merchants
    private static readonly HashSet<string> WhlistedMerchants = new()
    {
        "MERCHANT_VERIFIED_001",
        "MERCHANT_VERIFIED_002"
    };

    // Blacklist: yüksek risk merchants
    private static readonly HashSet<string> BlacklistedMerchants = new()
    {
        "MERCHANT_SUSPICIOUS_001",
        "MERCHANT_BANNED_002"
    };

    public string RuleName => "MerchantRiskAssessment";

    public MerchantRiskRule(ILogger<MerchantRiskRule> logger)
    {
        _logger = logger;
    }

    public Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        if (BlacklistedMerchants.Contains(context.MerchantId))
        {
            _logger.LogWarning("Blacklisted merchant detected: {MerchantId}", context.MerchantId);
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 95,
                Reason: "Merchant is blacklisted"));
        }

        if (WhlistedMerchants.Contains(context.MerchantId))
        {
            _logger.LogDebug("Whitelisted merchant: {MerchantId}", context.MerchantId);
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 5,
                Reason: "Merchant is trusted (whitelisted)"));
        }

        // Unknown merchant - moderate risk
        return Task.FromResult(new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: 30,
            Reason: "Merchant risk profile unknown"));
    }
}
