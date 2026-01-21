using MassTransit;

public sealed class TransactionOrchestrationState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }          // MassTransit saga correlation
    public string CurrentState { get; set; } = default!;

    public Guid TransactionId { get; set; }
    public string CorrelationKey { get; set; } = default!; // bizim string correlationId

    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public string MerchantId { get; set; } = default!;

    public int? RiskScore { get; set; }
    public string? FraudExplanation { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }


    public Guid? FraudTimeoutTokenId { get; set; }
    public int RetryCount { get; set; }
    public DateTime? TimedOutAtUtc { get; set; }

    public int Version { get; set; }

}