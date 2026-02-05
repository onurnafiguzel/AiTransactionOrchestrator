namespace Fraud.Worker.Rules;

public interface IFraudDetectionRule
{
    string RuleName { get; }
    Task<FraudRuleResult> EvaluateAsync(FraudDetectionContext context, CancellationToken ct);
}

public sealed record FraudDetectionContext(
    Guid TransactionId,
    string MerchantId,
    decimal Amount,
    string Currency,
    string CustomerIp,
    string? CustomerCountry,
    DateTime TransactionTime);

public sealed record FraudRuleResult(
    string RuleName,
    bool IsFraud,
    int RiskScore, // 0-100
    string Reason);
