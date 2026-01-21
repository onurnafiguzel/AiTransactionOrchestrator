namespace BuildingBlocks.Contracts.Fraud;

public sealed record FraudCheckTimeoutExpired(
    Guid TransactionId,
    string CorrelationId
);
