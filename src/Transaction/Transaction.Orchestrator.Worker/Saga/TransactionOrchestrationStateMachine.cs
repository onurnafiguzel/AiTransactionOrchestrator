using BuildingBlocks.Contracts.Fraud;
using BuildingBlocks.Contracts.Transactions;
using MassTransit;

namespace Transaction.Orchestrator.Worker.Saga;

public sealed class TransactionOrchestrationStateMachine : MassTransitStateMachine<TransactionOrchestrationState>
{
    private const int MaxRetry = 3;

    public State Submitted { get; private set; } = default!;
    public State FraudRequested { get; private set; } = default!;
    public State TimedOut { get; private set; } = default!;
    public State Completed { get; private set; } = default!;

    public Event<TransactionCreated> TransactionCreated { get; private set; } = default!;
    public Event<FraudCheckCompleted> FraudCheckCompleted { get; private set; } = default!;

    // Timeout schedule
    public Schedule<TransactionOrchestrationState, FraudCheckTimeoutExpired> FraudTimeout { get; private set; } = default!;

    public TransactionOrchestrationStateMachine()
    {
        Event(() => TransactionCreated, x =>
        {
            x.CorrelateById(ctx => ctx.Message.TransactionId);
            x.SelectId(ctx => ctx.Message.TransactionId);
        });

        Event(() => FraudCheckCompleted, x =>
        {
            x.CorrelateById(ctx => ctx.Message.TransactionId);
        });

        Schedule(() => FraudTimeout, x => x.FraudTimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromSeconds(30); // ilk timeout
            s.Received = e =>
                e.CorrelateById(ctx => ctx.Message.TransactionId);
        });

        InstanceState(x => x.CurrentState);

        Initially(
            When(TransactionCreated)
                .Then(ctx =>
                {
                    ctx.Saga.TransactionId = ctx.Message.TransactionId;
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                    ctx.Saga.MerchantId = ctx.Message.MerchantId;
                    ctx.Saga.CorrelationKey = ctx.Message.CorrelationId;
                    ctx.Saga.RetryCount = 0;
                    ctx.Saga.CreatedAtUtc = DateTime.UtcNow;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Submitted)
                .Publish(ctx => new FraudCheckRequested(
                    TransactionId: ctx.Saga.TransactionId,
                    Amount: ctx.Saga.Amount,
                    Currency: ctx.Saga.Currency,
                    MerchantId: ctx.Saga.MerchantId,
                    CorrelationId: ctx.Saga.CorrelationKey
                ))
                .TransitionTo(FraudRequested)
                .Schedule(FraudTimeout, ctx => new FraudCheckTimeoutExpired(
                    TransactionId: ctx.Saga.TransactionId,
                    CorrelationId: ctx.Saga.CorrelationKey
                ))
        );

        During(FraudRequested,
            When(FraudCheckCompleted)
                .Unschedule(FraudTimeout)
                .Then(ctx =>
                {
                    ctx.Saga.RiskScore = ctx.Message.RiskScore;
                    ctx.Saga.FraudExplanation = ctx.Message.Explanation;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                // Outcome publish: Approve / Reject
                .IfElse(ctx => ctx.Message.Decision == FraudDecision.Approve,
                    approved => approved
                        .Publish(ctx => new TransactionApproved(
                            TransactionId: ctx.Saga.TransactionId,
                            RiskScore: ctx.Message.RiskScore,
                            Explanation: ctx.Message.Explanation,
                            CorrelationId: ctx.Saga.CorrelationKey,
                            OccurredAtUtc: DateTime.UtcNow
                        ))
                        .TransitionTo(Completed)
                        .Finalize(),
                    rejected => rejected
                        .Publish(ctx => new TransactionRejected(
                            TransactionId: ctx.Saga.TransactionId,
                            RiskScore: ctx.Message.RiskScore,
                            Reason: "FraudDecisionReject",
                            Explanation: ctx.Message.Explanation,
                            CorrelationId: ctx.Saga.CorrelationKey,
                            OccurredAtUtc: DateTime.UtcNow
                        ))
                        .TransitionTo(Completed)
                        .Finalize()
                ),

            When(FraudTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.RetryCount += 1;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .IfElse(ctx => ctx.Saga.RetryCount <= MaxRetry,
                    retry => retry
                        .Publish(ctx => new FraudCheckRequested(
                            TransactionId: ctx.Saga.TransactionId,
                            Amount: ctx.Saga.Amount,
                            Currency: ctx.Saga.Currency,
                            MerchantId: ctx.Saga.MerchantId,
                            CorrelationId: ctx.Saga.CorrelationKey
                        ))
                        .Schedule(FraudTimeout,
                            ctx => new FraudCheckTimeoutExpired(
                                TransactionId: ctx.Saga.TransactionId,
                                CorrelationId: ctx.Saga.CorrelationKey
                            ),
                            ctx => TimeSpan.FromSeconds(30 * (int)Math.Pow(2, ctx.Saga.RetryCount - 1))
                        ),
                    giveup => giveup
                        .Then(ctx =>
                        {
                            ctx.Saga.TimedOutAtUtc = DateTime.UtcNow;
                            ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                        })
                        // Timeout final outcome: Rejected(TimedOut)
                        .Publish(ctx => new TransactionRejected(
                            TransactionId: ctx.Saga.TransactionId,
                            RiskScore: ctx.Saga.RiskScore ?? 0,
                            Reason: "TimedOut",
                            Explanation: "Fraud check did not complete within allowed retries/timeouts.",
                            CorrelationId: ctx.Saga.CorrelationKey,
                            OccurredAtUtc: DateTime.UtcNow
                        ))
                        .TransitionTo(TimedOut)
                        .Finalize()
                )
        );

        SetCompletedWhenFinalized();
    }
}
