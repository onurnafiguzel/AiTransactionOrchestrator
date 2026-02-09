using Microsoft.Extensions.Logging;

namespace Fraud.Worker.Rules;

/// <summary>
/// Kullanıcı konumu anomalisi - Kullanıcının anormal yerlerden işlem yapıp yapmadığını kontrol et
/// - Kullanıcının daha önceki işlemleri hangi ülkelerden yapması normal?
/// - Bu işlem alışılmadık bir ülkeden mi geliyor?
/// </summary>
public sealed class UserLocationAnomalyRule(
    ILogger<UserLocationAnomalyRule> logger) : IFraudDetectionRule
{
    public string RuleName => "UserLocationAnomaly";

    public Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct)
    {
        // TODO: Redis'ten kullanıcının işlem geçmişini (ülkeler) al
        // Redis key: user:transaction:countries:{userId}
        // Örnek: ["TR", "US", "DE"] - kullanıcının daha önceki işlemler yapması normal olan ülkeler

        if (string.IsNullOrWhiteSpace(context.CustomerCountry))
        {
            // Ülke bilgisi yoksa risk hesaplanamıyor
            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: false,
                RiskScore: 10,
                Reason: "Country information unavailable for anomaly detection"));
        }

        // TODO: Gerçek implementasyonda user history'den al
        var userNormalCountries = new[] { "TR", "US", "DE", "GB", "FR" }; // Örnek
        var isAnomalousLocation = !userNormalCountries.Contains(context.CustomerCountry);

        if (isAnomalousLocation)
        {
            logger.LogWarning(
                "Anomalous location detected for user {UserId}: {Country}. Normal countries: {NormalCountries}",
                context.UserId, context.CustomerCountry, string.Join(", ", userNormalCountries));

            return Task.FromResult(new FraudRuleResult(
                RuleName,
                IsFraud: false,  // Anomaly ≠ fraud, ama risk var
                RiskScore: 55,
                Reason: $"Transaction from unusual location: {context.CustomerCountry}. User normally transacts from: {string.Join(", ", userNormalCountries)}"));
        }

        logger.LogDebug(
            "User {UserId} location is normal: {Country}",
            context.UserId, context.CustomerCountry);

        return Task.FromResult(new FraudRuleResult(
            RuleName,
            IsFraud: false,
            RiskScore: 5,
            Reason: $"Transaction location is within user's normal pattern: {context.CustomerCountry}"));
    }
}
