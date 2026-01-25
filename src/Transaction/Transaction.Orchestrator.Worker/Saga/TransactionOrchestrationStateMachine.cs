using BuildingBlocks.Contracts.Fraud;
using BuildingBlocks.Contracts.Observability;
using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using System;
using System.Text.Json;
using System.Threading;
using Transaction.Orchestrator.Worker.Timeline;

namespace Transaction.Orchestrator.Worker.Saga;

public sealed class TransactionOrchestrationStateMachine : MassTransitStateMachine<TransactionOrchestrationState>
{
    private readonly ILogger<TransactionOrchestrationStateMachine> _logger;

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

        //// Timeout event correlation (some setups still need this)
        //Event(() => FraudTimeout.Received, x =>
        //{
        //    x.CorrelateById(ctx => ctx.Message.TransactionId);
        //});

        Schedule(() => FraudTimeout, x => x.FraudTimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromSeconds(30);
        });

        InstanceState(x => x.CurrentState);

        Initially(
            When(TransactionCreated)
                        .Then(ctx =>
                        {
                            var cid = GetCorrelationId(ctx, ctx.Message.CorrelationId);

                            ctx.Saga.TransactionId = ctx.Message.TransactionId;
                            ctx.Saga.Amount = ctx.Message.Amount;
                            ctx.Saga.Currency = ctx.Message.Currency;
                            ctx.Saga.MerchantId = ctx.Message.MerchantId;

                            ctx.Saga.CorrelationKey = cid;

                            ctx.Saga.RetryCount = 0;
                            ctx.Saga.CreatedAtUtc = DateTime.UtcNow;
                            ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                        })
                        .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                        ctx,
                        eventType: "TransactionCreated",
                        details: new
                        {
                            amount = ctx.Saga.Amount,
                            currency = ctx.Saga.Currency,
                            merchantId = ctx.Saga.MerchantId
                        });
                        }))
                        .TransitionTo(Submitted)
                        .ThenAsync(async ctx =>
                        {
                            // Publish FraudCheckRequested + header
                            var msg = new FraudCheckRequested(
                        TransactionId: ctx.Saga.TransactionId,
                        Amount: ctx.Saga.Amount,
                        Currency: ctx.Saga.Currency,
                        MerchantId: ctx.Saga.MerchantId,
                        CorrelationId: ctx.Saga.CorrelationKey);

                            await ctx.Publish(msg, pub =>
                    {
                        pub.Headers.Set(Correlation.HeaderName, ctx.Saga.CorrelationKey);
                    });
                        })
                        .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                        ctx,
                        eventType: "FraudCheckRequested",
                        details: new
                        {
                            amount = ctx.Saga.Amount,
                            currency = ctx.Saga.Currency,
                            merchantId = ctx.Saga.MerchantId,
                            retry = ctx.Saga.RetryCount
                        });
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
                    var cid = GetCorrelationId(ctx, ctx.Message.CorrelationId);

                    if (string.IsNullOrWhiteSpace(ctx.Saga.CorrelationKey))
                        ctx.Saga.CorrelationKey = cid;

                    ctx.Saga.RiskScore = ctx.Message.RiskScore;
                    ctx.Saga.FraudExplanation = ctx.Message.Explanation;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                {
                    await AppendTimeline(
                        ctx,
                        eventType: "FraudCheckCompleted",
                        details: new
                        {
                            decision = ctx.Message.Decision.ToString(),
                            riskScore = ctx.Message.RiskScore
                        });
                }))
                .IfElse(ctx => ctx.Message.Decision == FraudDecision.Approve,
                    approved => approved
                        .ThenAsync(async ctx =>
                        {
                            var msg = new TransactionApproved(
                                TransactionId: ctx.Saga.TransactionId,
                                RiskScore: ctx.Message.RiskScore,
                                Explanation: ctx.Message.Explanation,
                                CorrelationId: ctx.Saga.CorrelationKey,
                                OccurredAtUtc: DateTime.UtcNow);

                            await ctx.Publish(msg, pub =>
                            {
                                pub.Headers.Set(Correlation.HeaderName, ctx.Saga.CorrelationKey);
                            });
                        })
                        .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "TransactionApproved",
                                details: new
                                {
                                    riskScore = ctx.Message.RiskScore
                                });
                        }))
                        .TransitionTo(Completed)
                        .Finalize(),
                    rejected => rejected
                        .ThenAsync(async ctx =>
                        {
                            var msg = new TransactionRejected(
                                TransactionId: ctx.Saga.TransactionId,
                                RiskScore: ctx.Message.RiskScore,
                                Reason: "FraudDecisionReject",
                                Explanation: ctx.Message.Explanation,
                                CorrelationId: ctx.Saga.CorrelationKey,
                                OccurredAtUtc: DateTime.UtcNow);

                            await ctx.Publish(msg, pub =>
                            {
                                pub.Headers.Set(Correlation.HeaderName, ctx.Saga.CorrelationKey);
                            });
                        })
                        .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "TransactionRejected",
                                details: new
                                {
                                    reason = "FraudDecisionReject",
                                    riskScore = ctx.Message.RiskScore
                                });
                        }))
                        .TransitionTo(Completed)
                        .Finalize()
                ),

            When(FraudTimeout.Received)
                .Then(ctx =>
                {
                    var cid = GetCorrelationId(ctx, ctx.Message.CorrelationId);

                    if (string.IsNullOrWhiteSpace(ctx.Saga.CorrelationKey))
                        ctx.Saga.CorrelationKey = cid;

                    ctx.Saga.RetryCount += 1;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                {
                    await AppendTimeline(
                        ctx,
                        eventType: "FraudTimeoutExpired",
                        details: new
                        {
                            retry = ctx.Saga.RetryCount
                        });
                }))
                .IfElse(ctx => ctx.Saga.RetryCount <= MaxRetry,
                    retry => retry
                        .ThenAsync(async ctx =>
                        {
                            var msg = new FraudCheckRequested(
                                TransactionId: ctx.Saga.TransactionId,
                                Amount: ctx.Saga.Amount,
                                Currency: ctx.Saga.Currency,
                                MerchantId: ctx.Saga.MerchantId,
                                CorrelationId: ctx.Saga.CorrelationKey);

                            await ctx.Publish(msg, pub =>
                            {
                                pub.Headers.Set(Correlation.HeaderName, ctx.Saga.CorrelationKey);
                            });
                        })
                        .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "FraudCheckRequested",
                                details: new
                                {
                                    amount = ctx.Saga.Amount,
                                    currency = ctx.Saga.Currency,
                                    merchantId = ctx.Saga.MerchantId,
                                    retry = ctx.Saga.RetryCount
                                });
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
                            var cid = GetCorrelationId(ctx, null);

                            if (string.IsNullOrWhiteSpace(ctx.Saga.CorrelationKey))
                                ctx.Saga.CorrelationKey = cid;

                            ctx.Saga.TimedOutAtUtc = DateTime.UtcNow;
                            ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                        })
                        .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "TransactionTimedOut",
                                details: new
                                {
                                    retry = ctx.Saga.RetryCount,
                                    maxRetry = MaxRetry
                                });
                        }))
                        .ThenAsync(async ctx =>
                        {
                            var msg = new TransactionRejected(
                                TransactionId: ctx.Saga.TransactionId,
                                RiskScore: ctx.Saga.RiskScore ?? 0,
                                Reason: "TimedOut",
                                Explanation: "Fraud check did not complete within allowed retries/timeouts.",
                                CorrelationId: ctx.Saga.CorrelationKey,
                                OccurredAtUtc: DateTime.UtcNow);

                            await ctx.Publish(msg, pub =>
                            {
                                pub.Headers.Set(Correlation.HeaderName, ctx.Saga.CorrelationKey);
                            });
                        })
                        .ThenAsync(ctx => WithCorrelationLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "TransactionRejected",
                                details: new
                                {
                                    reason = "TimedOut",
                                    riskScore = ctx.Saga.RiskScore ?? 0
                                });
                        }))
                        .TransitionTo(TimedOut)
                        .Finalize()
                )
        );

        SetCompletedWhenFinalized();
    }

    // ---- KEEP: AppendTimeline MUST NOT BE REMOVED ----
    private Task AppendTimeline<T>(BehaviorContext<TransactionOrchestrationState, T> ctx, string eventType, object? details)
        where T : class
    {
        var json = details is null
            ? null
            : JsonSerializer.Serialize(details, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return timeline.Append(
            transactionId: ctx.Saga.TransactionId,
            eventType: eventType,
            detailsJson: json,
            correlationId: ctx.Saga.CorrelationKey,
            source: "orchestrator",
            ct: ctx.CancellationToken);
    }

    private static string GetCorrelationId<T>(
        BehaviorContext<TransactionOrchestrationState, T> ctx,
        string? messageCorrelationIdFallback)
        where T : class
    {
        // 1) Header'dan al (OutboxPublisherService burada basıyor)
        if (ctx.TryGetPayload<ConsumeContext>(out var consumeCtx))
        {
            var headerCid = consumeCtx.Headers.Get<string>(Correlation.HeaderName);
            if (!string.IsNullOrWhiteSpace(headerCid))
            {
                CorrelationContext.CorrelationId = headerCid;
                return headerCid;
            }

            // 2) MassTransit CorrelationId (Guid)
            if (consumeCtx.CorrelationId.HasValue)
            {
                var mtCid = consumeCtx.CorrelationId.Value.ToString("N");
                CorrelationContext.CorrelationId = mtCid;
                return mtCid;
            }
        }

        // 3) Message body fallback
        if (!string.IsNullOrWhiteSpace(messageCorrelationIdFallback))
        {
            CorrelationContext.CorrelationId = messageCorrelationIdFallback;
            return messageCorrelationIdFallback;
        }

        // 4) Generate
        var newCid = Guid.NewGuid().ToString("N");
        CorrelationContext.CorrelationId = newCid;
        return newCid;
    }

    private static Task WithCorrelationLogScope<T>(
        BehaviorContext<TransactionOrchestrationState, T> ctx,
        Func<Task> action)
        where T : class
    {
        var cid = string.IsNullOrWhiteSpace(ctx.Saga.CorrelationKey)
            ? GetCorrelationId(ctx, null)
            : ctx.Saga.CorrelationKey;

        using (Serilog.Context.LogContext.PushProperty("transaction_id", ctx.Saga.TransactionId))
        using (Serilog.Context.LogContext.PushProperty("state", ctx.Saga.CurrentState))
        {
            return action();
        }
    }

    // -------------------------------------------------
    // LOGGING HELPERS
    // -------------------------------------------------
    private IDisposable BeginSagaScope(BehaviorContext<TransactionOrchestrationState> ctx)
    {
        return _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = ctx.Instance.CorrelationId
        });
    }

    private void LogTransition(string from, string to)
    {
        _logger.LogInformation(
            "Saga transition | {From} -> {To}",
            from,
            to);
    }
}
