using Fraud.Worker.Caching;

namespace Fraud.Worker.Rules;

/// <summary>
/// Coğrafi risk taraması - Redis-backed ülke risk skorlarını kullanarak kontrol et
/// 
/// UserId kullanımı:
/// - User-specific country restrictions (kullanıcılar belirli ülkelerde işlem yapamaz)
/// - User travel patterns (kullanıcının daha önceki işlemleri hangi ülkelerden?)
/// - Redis: user:restricted:countries:{userId} → SET of forbidden countries
/// </summary>
public sealed class GeographicRiskRule(
    IGeographicRiskCacheService geoCache,
    IUserGeographicRestrictionCacheService userGeoCache,
    ILogger<GeographicRiskRule> logger) : IFraudDetectionRule
{
    private const int HighRiskThreshold = 70;
    private const int DefaultUnknownCountryRisk = 25;
    private const int DefaultMissingCountryRisk = 20;

    public string RuleName => "GeographicRiskAssessment";

    public async Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.CustomerCountry))
        {
            return new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: DefaultMissingCountryRisk,
                Reason: "Country information unavailable");
        }

        // Check user-specific country restrictions
        if (await userGeoCache.IsRestrictedCountryAsync(context.UserId, context.CustomerCountry, ct))
        {
            return new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 95,
                Reason: $"Country {context.CustomerCountry} is restricted for this user");
        }

        // Get risk score from Redis HASH (global country risks)
        var riskScore = await geoCache.GetCountryRiskScoreAsync(context.CustomerCountry, ct);

        if (!riskScore.HasValue)
        {
            logger.LogDebug("Country {Country} not in cache, using default risk", context.CustomerCountry);
            return new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: DefaultUnknownCountryRisk,
                Reason: $"Country {context.CustomerCountry} risk profile unknown");
        }

        var isFraud = riskScore.Value >= HighRiskThreshold;
        var riskLevel = riskScore.Value switch
        {
            >= 70 => "high-risk",
            >= 40 => "moderate-risk",
            _ => "low-risk"
        };

        if (isFraud)
        {
            logger.LogWarning("High-risk country detected for user {UserId}: {Country} (Score: {Score})",
                context.UserId, context.CustomerCountry, riskScore.Value);
        }

        return new FraudRuleResult(
            RuleName,
            IsFraud: isFraud,
            RiskScore: riskScore.Value,
            Reason: $"Transaction from {riskLevel} country: {context.CustomerCountry} (Score: {riskScore.Value})");
    }
}
