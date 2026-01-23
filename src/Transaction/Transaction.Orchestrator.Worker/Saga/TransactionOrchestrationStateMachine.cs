using System.Text.Json;
using BuildingBlocks.Contracts.Fraud;
using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using Transaction.Orchestrator.Worker.Timeline;

namespace Transaction.Orchestrator.Worker.Saga;

public sealed class TransactionOrchestrationStateMachine : MassTransitStateMachine<TransactionOrchestrationState>
{
    private readonly TimelineWriter timeline;

    private const int MaxRetry = 3;

    public State Submitted { get; private set; } = default!;
    public State FraudRequested { get; private set; } = default!;
    public State TimedOut { get; private set; } = default!;
    public State Completed { get; private set; } = default!;

    public Event<TransactionCreated> TransactionCreated { get; private set; } = default!;
    public Event<FraudCheckCompleted> FraudCheckCompleted { get; private set; } = default!;

    // Timeout schedule
    public Schedule<TransactionOrchestrationState, FraudCheckTimeoutExpired> FraudTimeout { get; private set; } = default!;

    public TransactionOrchestrationStateMachine(TimelineWriter timeline)
        : base()
    {
        this.timeline = timeline;

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
            s.Delay = TimeSpan.FromSeconds(30);
            s.Received = e => e.CorrelateById(ctx => ctx.Message.TransactionId);
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
                .ThenAsync(ctx => AppendTimeline(
                    ctx,
                    eventType: "TransactionCreated",
                    details: new
                    {
                        amount = ctx.Saga.Amount,
                        currency = ctx.Saga.Currency,
                        merchantId = ctx.Saga.MerchantId
                    }))
                .TransitionTo(Submitted)
                .Publish(ctx => new FraudCheckRequested(
                    TransactionId: ctx.Saga.TransactionId,
                    Amount: ctx.Saga.Amount,
                    Currency: ctx.Saga.Currency,
                    MerchantId: ctx.Saga.MerchantId,
                    CorrelationId: ctx.Saga.CorrelationKey
                ))
                .ThenAsync(ctx => AppendTimeline(
                    ctx,
                    eventType: "FraudCheckRequested",
                    details: new
                    {
                        amount = ctx.Saga.Amount,
                        currency = ctx.Saga.Currency,
                        merchantId = ctx.Saga.MerchantId,
                        retry = ctx.Saga.RetryCount
                    }))
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
                .ThenAsync(ctx => AppendTimeline(
                    ctx,
                    eventType: "FraudCheckCompleted",
                    details: new
                    {
                        decision = ctx.Message.Decision.ToString(),
                        riskScore = ctx.Message.RiskScore
                    }))
                .IfElse(ctx => ctx.Message.Decision == FraudDecision.Approve,
                    approved => approved
                        .Publish(ctx => new TransactionApproved(
                            TransactionId: ctx.Saga.TransactionId,
                            RiskScore: ctx.Message.RiskScore,
                            Explanation: ctx.Message.Explanation,
                            CorrelationId: ctx.Saga.CorrelationKey,
                            OccurredAtUtc: DateTime.UtcNow
                        ))
                        .ThenAsync(ctx => AppendTimeline(
                            ctx,
                            eventType: "TransactionApproved",
                            details: new
                            {
                                riskScore = ctx.Message.RiskScore
                            }))
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
                        .ThenAsync(ctx => AppendTimeline(
                            ctx,
                            eventType: "TransactionRejected",
                            details: new
                            {
                                reason = "FraudDecisionReject",
                                riskScore = ctx.Message.RiskScore
                            }))
                        .TransitionTo(Completed)
                        .Finalize()
                ),

            When(FraudTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.RetryCount += 1;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .ThenAsync(ctx => AppendTimeline(
                    ctx,
                    eventType: "FraudTimeoutExpired",
                    details: new
                    {
                        retry = ctx.Saga.RetryCount
                    }))
                .IfElse(ctx => ctx.Saga.RetryCount <= MaxRetry,
                    retry => retry
                        .Publish(ctx => new FraudCheckRequested(
                            TransactionId: ctx.Saga.TransactionId,
                            Amount: ctx.Saga.Amount,
                            Currency: ctx.Saga.Currency,
                            MerchantId: ctx.Saga.MerchantId,
                            CorrelationId: ctx.Saga.CorrelationKey
                        ))
                        .ThenAsync(ctx => AppendTimeline(
                            ctx,
                            eventType: "FraudCheckRequested",
                            details: new
                            {
                                amount = ctx.Saga.Amount,
                                currency = ctx.Saga.Currency,
                                merchantId = ctx.Saga.MerchantId,
                                retry = ctx.Saga.RetryCount
                            }))
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
                        .ThenAsync(ctx => AppendTimeline(
                            ctx,
                            eventType: "TransactionTimedOut",
                            details: new
                            {
                                retry = ctx.Saga.RetryCount,
                                maxRetry = MaxRetry
                            }))
                        .Publish(ctx => new TransactionRejected(
                            TransactionId: ctx.Saga.TransactionId,
                            RiskScore: ctx.Saga.RiskScore ?? 0,
                            Reason: "TimedOut",
                            Explanation: "Fraud check did not complete within allowed retries/timeouts.",
                            CorrelationId: ctx.Saga.CorrelationKey,
                            OccurredAtUtc: DateTime.UtcNow
                        ))
                        .ThenAsync(ctx => AppendTimeline(
                            ctx,
                            eventType: "TransactionRejected",
                            details: new
                            {
                                reason = "TimedOut",
                                riskScore = ctx.Saga.RiskScore ?? 0
                            }))
                        .TransitionTo(TimedOut)
                        .Finalize()
                )
        );
    }

    private Task AppendTimeline<T>(BehaviorContext<TransactionOrchestrationState, T> ctx, string eventType, object? details)
        where T : class
    {
        var json = details is null ? null : JsonSerializer.Serialize(details, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return timeline.Append(
            transactionId: ctx.Saga.TransactionId,
            eventType: eventType,
            detailsJson: json,
            correlationId: ctx.Saga.CorrelationKey,
            source: "orchestrator",
            ct: ctx.CancellationToken);
    }
}
