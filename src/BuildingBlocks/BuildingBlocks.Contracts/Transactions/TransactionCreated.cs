namespace BuildingBlocks.Contracts.Transactions;

public sealed record TransactionCreated(
    Guid TransactionId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string MerchantId,
    string CorrelationId,
    string CustomerIp = "0.0.0.0"  // Client IP for fraud detection
);
