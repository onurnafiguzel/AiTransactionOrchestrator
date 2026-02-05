using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Coğrafi risk taraması - suspicious ülkelerden gelen işlemleri kontrol et
/// </summary>
public sealed class GeographicRiskRule : IFraudDetectionRule
{
    private readonly ILogger<GeographicRiskRule> _logger;

    // Yüksek risk ülkeler
    private static readonly HashSet<string> HighRiskCountries = new()
    {
        "KP", // North Korea
        "IR", // Iran
        "SY", // Syria
        "CU"  // Cuba
    };

    // Moderate risk ülkeler
    private static readonly HashSet<string> ModerateRiskCountries = new()
    {
        "RU", // Russia
        "KZ", // Kazakhstan
        "UZ"  // Uzbekistan
    };

    public string RuleName => "GeographicRiskAssessment";

    public GeographicRiskRule(ILogger<GeographicRiskRule> logger)
    {
        _logger = logger;
    }

    public Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.CustomerCountry))
        {
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 20,
                Reason: "Country information unavailable"));
        }

        if (HighRiskCountries.Contains(context.CustomerCountry))
        {
            _logger.LogWarning("High-risk country detected: {Country}", context.CustomerCountry);
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: true,
                RiskScore: 80,
                Reason: $"Transaction from high-risk country: {context.CustomerCountry}"));
        }

        if (ModerateRiskCountries.Contains(context.CustomerCountry))
        {
            _logger.LogDebug("Moderate-risk country: {Country}", context.CustomerCountry);
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 45,
                Reason: $"Transaction from moderate-risk country: {context.CustomerCountry}"));
        }

        return Task.FromResult(new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: 10,
            Reason: $"Safe country detected: {context.CustomerCountry}"));
    }
}
