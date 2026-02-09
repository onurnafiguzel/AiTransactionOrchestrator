using Microsoft.Extensions.Logging;
using Fraud.Worker.Caching;

namespace Fraud.Worker.Rules;

/// <summary>
/// Kullanıcı konumu anomalisi - Kullanıcının anormal yerlerden işlem yapıp yapmadığını kontrol et
/// - Kullanıcının daha önceki işlemleri hangi ülkelerden yapması normal?
/// - Bu işlem alışılmadık bir ülkeden mi geliyor?
/// </summary>
public sealed class UserLocationAnomalyRule(
    IUserGeographicRestrictionCacheService userGeoCache,
    ILogger<UserLocationAnomalyRule> logger) : IFraudDetectionRule
{
    public string RuleName => "UserLocationAnomaly";

    public async Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.CustomerCountry))
        {
            // Ülke bilgisi yoksa risk hesaplanamıyor
            return new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 10,
                Reason: "Country information unavailable for anomaly detection");
        }

        // Get user's transaction history from Redis
        var userNormalCountries = await userGeoCache.GetUserTransactionCountriesAsync(context.UserId, ct);
        
        // If user has no history, consider all countries normal
        if (userNormalCountries.Length == 0)
        {
            // Record this country in user's history
            await userGeoCache.AddUserTransactionCountryAsync(context.UserId, context.CustomerCountry, ct);
            return new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 15,
                Reason: $"First transaction from {context.CustomerCountry} - building user location profile");
        }

        var isAnomalousLocation = !userNormalCountries.Contains(context.CustomerCountry);

        if (isAnomalousLocation)
        {
            logger.LogWarning(
                "Anomalous location detected for user {UserId}: {Country}. Normal countries: {NormalCountries}",
                context.UserId, context.CustomerCountry, string.Join(", ", userNormalCountries));

            // Still record this as a transaction country for future reference
            await userGeoCache.AddUserTransactionCountryAsync(context.UserId, context.CustomerCountry, ct);

            return new FraudRuleResult(
                RuleName,
                IsFraud: false,  // Anomaly ≠ fraud, ama risk var
                RiskScore: 55,
                Reason: $"Transaction from unusual location: {context.CustomerCountry}. User normally transacts from: {string.Join(", ", userNormalCountries)}");
        }

        logger.LogDebug(
            "User {UserId} location is normal: {Country}",
            context.UserId, context.CustomerCountry);

        return new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: 5,
            Reason: $"Transaction location is within user's normal pattern: {context.CustomerCountry}");
    }
}
