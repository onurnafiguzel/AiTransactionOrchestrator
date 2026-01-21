namespace BuildingBlocks.Contracts.Fraud;

public sealed record FraudCheckRequested(
    Guid TransactionId,
    decimal Amount,
    string Currency,
    string MerchantId,
    string CorrelationId
);