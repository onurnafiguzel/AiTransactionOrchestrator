using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Coğrafi risk taraması - Redis-backed ülke risk skorlarını kullanarak kontrol et
/// </summary>
public sealed class GeographicRiskRule : IFraudDetectionRule
{
    private readonly IGeographicRiskCacheService _geoCache;
    private readonly ILogger<GeographicRiskRule> _logger;

    private const int HighRiskThreshold = 70;
    private const int DefaultUnknownCountryRisk = 25;
    private const int DefaultMissingCountryRisk = 20;

    public string RuleName => "GeographicRiskAssessment";

    public GeographicRiskRule(
        IGeographicRiskCacheService geoCache,
        ILogger<GeographicRiskRule> logger)
    {
        _geoCache = geoCache;
        _logger = logger;
    }

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

        // Get risk score from Redis HASH
        var riskScore = await _geoCache.GetCountryRiskScoreAsync(context.CustomerCountry, ct);

        if (!riskScore.HasValue)
        {
            _logger.LogDebug("Country {Country} not in cache, using default risk", context.CustomerCountry);
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
            _logger.LogWarning("High-risk country detected: {Country} (Score: {Score})",
                context.CustomerCountry, riskScore.Value);
        }

        return new FraudRuleResult(
            RuleName,
            IsFraud: isFraud,
            RiskScore: riskScore.Value,
            Reason: $"Transaction from {riskLevel} country: {context.CustomerCountry} (Score: {riskScore.Value})");
    }
}
