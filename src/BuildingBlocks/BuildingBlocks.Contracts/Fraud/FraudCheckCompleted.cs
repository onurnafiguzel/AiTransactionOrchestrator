namespace BuildingBlocks.Contracts.Fraud;

public sealed record FraudCheckCompleted(
    Guid TransactionId,
    int RiskScore,
    FraudDecision Decision,
    string Explanation,
    string CorrelationId
);