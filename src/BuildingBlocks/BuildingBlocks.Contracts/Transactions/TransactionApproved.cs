namespace BuildingBlocks.Contracts.Transactions;

public sealed record TransactionApproved(
    Guid TransactionId,
    int RiskScore,
    string Explanation,
    string CorrelationId,
    DateTime OccurredAtUtc
);