namespace BuildingBlocks.Contracts.Fraud;

public sealed record FraudCheckRequested(
    Guid TransactionId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string MerchantId,
    string CorrelationId,
    string CustomerIp = "0.0.0.0"  // Client IP for IP-based fraud detection
);