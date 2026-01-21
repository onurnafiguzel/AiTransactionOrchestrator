namespace BuildingBlocks.Contracts.Transactions;

public sealed record TransactionRejected(
    Guid TransactionId,
    int RiskScore,
    string Reason,
    string Explanation,
    string CorrelationId,
    DateTime OccurredAtUtc
);
