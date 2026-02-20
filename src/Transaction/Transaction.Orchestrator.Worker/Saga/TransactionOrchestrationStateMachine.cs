using BuildingBlocks.Contracts.Fraud;
using BuildingBlocks.Contracts.Observability;
using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using System.Text.Json;
using Transaction.Orchestrator.Worker.Timeline;

namespace Transaction.Orchestrator.Worker.Saga;

internal sealed class NullDisposable : IDisposable
{
    public static readonly NullDisposable Instance = new();
    
    public void Dispose()
    {
        // No-op disposable
    }
}

public sealed class TransactionOrchestrationStateMachine : MassTransitStateMachine<TransactionOrchestrationState>
{
    private readonly ILogger<TransactionOrchestrationStateMachine> logger;
    private readonly TimelineWriter timeline;
    private const int MaxRetry = 3;   

    public State Submitted { get; private set; } = default!;
    public State FraudRequested { get; private set; } = default!;
    public State TimedOut { get; private set; } = default!;
    public State Completed { get; private set; } = default!;

    public Event<TransactionCreated> TransactionCreated { get; private set; } = default!;
    public Event<FraudCheckCompleted> FraudCheckCompleted { get; private set; } = default!;

    public Schedule<TransactionOrchestrationState, FraudCheckTimeoutExpired> FraudTimeout { get; private set; } = default!;

    public TransactionOrchestrationStateMachine(
        TimelineWriter timeline,
        ILogger<TransactionOrchestrationStateMachine> logger)
        : base()
    {
        this.timeline = timeline;
        this.logger = logger;

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
        });

        InstanceState(x => x.CurrentState);

        Initially(
            When(TransactionCreated)
                .Then(ctx =>
                {
                    var cid = GetCorrelationId(ctx, ctx.Message.CorrelationId);

                    ctx.Saga.TransactionId = ctx.Message.TransactionId;
                    ctx.Saga.UserId = ctx.Message.UserId;
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                    ctx.Saga.MerchantId = ctx.Message.MerchantId;
                    ctx.Saga.CustomerIp = ctx.Message.CustomerIp;  // ← Store IP from event

                    ctx.Saga.CorrelationKey = cid;

                    ctx.Saga.RetryCount = 0;
                    ctx.Saga.CreatedAtUtc = DateTime.UtcNow;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;

                    using (BeginSagaScope(ctx))
                    {
                        logger.LogInformation(
                            "Saga started | Transaction Id={TransactionId}",
                            ctx.Saga.TransactionId);

                        LogTransition(nameof(Initial), nameof(Submitted));
                    }
                })
                .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
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

                    logger.LogInformation("Timeline appended | Event=TransactionCreated");
                }))
                .TransitionTo(Submitted)
                .ThenAsync(async ctx =>
                {
                    var msg = new FraudCheckRequested(
                        TransactionId: ctx.Saga.TransactionId,
                        UserId: ctx.Saga.UserId,
                        Amount: ctx.Saga.Amount,
                        Currency: ctx.Saga.Currency,
                        MerchantId: ctx.Saga.MerchantId,
                        CorrelationId: ctx.Saga.CorrelationKey,
                        CustomerIp: ctx.Saga.CustomerIp);

                    await ctx.Publish(msg, pub =>
                    {
                        pub.Headers.Set(Correlation.HeaderName, ctx.Saga.CorrelationKey);
                    });

                    using (BeginSagaScope(ctx))
                    {
                        logger.LogInformation(
                            "Published FraudCheckRequested | Retry={Retry}",                            
                            ctx.Saga.RetryCount);

                        LogTransition(nameof(Submitted), nameof(FraudRequested));
                    }
                })
                .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
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

                    logger.LogInformation("Timeline appended | Event=FraudCheckRequested");
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

                    using (BeginSagaScope(ctx))
                    {
                        logger.LogInformation(
                            "FraudCheckCompleted received | Decision={Decision} | RiskScore={RiskScore}",
                            ctx.Message.Decision,
                            ctx.Message.RiskScore);
                    }
                })
                .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
                {
                    await AppendTimeline(
                        ctx,
                        eventType: "FraudCheckCompleted",
                        details: new
                        {
                            decision = ctx.Message.Decision.ToString(),
                            riskScore = ctx.Message.RiskScore
                        });

                    logger.LogInformation("Timeline appended | Event=FraudCheckCompleted");
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

                            using (BeginSagaScope(ctx))
                            {
                                logger.LogInformation(
                                    "Published TransactionApproved | RiskScore={RiskScore}",
                                    ctx.Message.RiskScore);

                                LogTransition(nameof(FraudRequested), nameof(Completed));
                            }
                        })
                        .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "TransactionApproved",
                                details: new
                                {
                                    riskScore = ctx.Message.RiskScore
                                });

                            logger.LogInformation("Timeline appended | Event=TransactionApproved");
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

                            using (BeginSagaScope(ctx))
                            {
                                logger.LogInformation(
                                    "Published TransactionRejected | Reason={Reason} | Decision={Decision} | RiskScore={RiskScore}",
                                    "FraudDecisionReject",
                                    ctx.Message.Decision,
                                    ctx.Message.RiskScore);

                                LogTransition(nameof(FraudRequested), nameof(Completed));
                            }
                        })
                        .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "TransactionRejected",
                                details: new
                                {
                                    reason = "FraudDecisionReject",
                                    riskScore = ctx.Message.RiskScore
                                });

                            logger.LogInformation("Timeline appended | Event=TransactionRejected");
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

                    using (BeginSagaScope(ctx))
                    {
                        logger.LogWarning(
                            "Fraud timeout expired | Retry={Retry} | MaxRetry={MaxRetry}",
                            ctx.Saga.RetryCount,
                            MaxRetry);
                    }
                })
                .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
                {
                    await AppendTimeline(
                        ctx,
                        eventType: "FraudTimeoutExpired",
                        details: new
                        {
                            retry = ctx.Saga.RetryCount
                        });

                    logger.LogInformation("Timeline appended | Event=FraudTimeoutExpired");
                }))
                .IfElse(ctx => ctx.Saga.RetryCount <= MaxRetry,

                    retry => retry
                        .ThenAsync(async ctx =>
                        {
                            var msg = new FraudCheckRequested(
                                TransactionId: ctx.Saga.TransactionId,
                                UserId: ctx.Saga.UserId,
                                Amount: ctx.Saga.Amount,
                                Currency: ctx.Saga.Currency,
                                MerchantId: ctx.Saga.MerchantId,
                                CorrelationId: ctx.Saga.CorrelationKey,
                                CustomerIp: ctx.Saga.CustomerIp);

                            await ctx.Publish(msg, pub =>
                            {
                                pub.Headers.Set(Correlation.HeaderName, ctx.Saga.CorrelationKey);
                            });

                            using (BeginSagaScope(ctx))
                            {
                                logger.LogInformation(
                                    "Retry publish FraudCheckRequested | Retry={Retry}",
                                    ctx.Saga.RetryCount);
                            }
                        })
                        .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
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

                            logger.LogInformation("Timeline appended | Event=FraudCheckRequested");
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

                            using (BeginSagaScope(ctx))
                            {
                                logger.LogError(
                                    "Saga timed out | Retry={Retry} | MaxRetry={MaxRetry}",
                                    ctx.Saga.RetryCount,
                                    MaxRetry);

                                LogTransition(nameof(FraudRequested), nameof(TimedOut));
                            }
                        })
                        .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "TransactionTimedOut",
                                details: new
                                {
                                    retry = ctx.Saga.RetryCount,
                                    maxRetry = MaxRetry
                                });

                            logger.LogInformation("Timeline appended | Event=TransactionTimedOut");
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

                            using (BeginSagaScope(ctx))
                            {
                                logger.LogInformation(
                                    "Published TransactionRejected | Reason={Reason}",
                                    "TimedOut");
                            }
                        })
                        .ThenAsync(ctx => WithSagaLogScope(ctx, async () =>
                        {
                            await AppendTimeline(
                                ctx,
                                eventType: "TransactionRejected",
                                details: new
                                {
                                    reason = "TimedOut",
                                    riskScore = ctx.Saga.RiskScore ?? 0
                                });

                            logger.LogInformation("Timeline appended | Event=TransactionRejected");
                        }))
                        .TransitionTo(TimedOut)
                        .Finalize()
                )
        );

        SetCompletedWhenFinalized(); // Kaydı tamamlanan saga örneklerini otomatik olarak sil
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
        if (ctx.TryGetPayload<ConsumeContext>(out var consumeCtx))
        {
            var headerCid = consumeCtx.Headers.Get<string>(Correlation.HeaderName);
            if (!string.IsNullOrWhiteSpace(headerCid))
            {
                CorrelationContext.CorrelationId = headerCid;
                return headerCid;
            }

            if (consumeCtx.CorrelationId.HasValue)
            {
                var mtCid = consumeCtx.CorrelationId.Value.ToString("N");
                CorrelationContext.CorrelationId = mtCid;
                return mtCid;
            }
        }

        if (!string.IsNullOrWhiteSpace(messageCorrelationIdFallback))
        {
            CorrelationContext.CorrelationId = messageCorrelationIdFallback;
            return messageCorrelationIdFallback;
        }

        var newCid = Guid.NewGuid().ToString("N");
        CorrelationContext.CorrelationId = newCid;
        return newCid;
    }

    private Task WithSagaLogScope<T>(BehaviorContext<TransactionOrchestrationState, T> ctx, Func<Task> action)
        where T : class
    {
        var cid = string.IsNullOrWhiteSpace(ctx.Saga.CorrelationKey)
            ? GetCorrelationId(ctx, null)
            : ctx.Saga.CorrelationKey;

        // correlation_id zaten global enricher ile basılıyor; burada ekstra bağlam ekliyoruz
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["correlation_id"] = cid,
            ["transaction_id"] = ctx.Saga.TransactionId,
            ["state"] = ctx.Saga.CurrentState
        }))
        {
            return action();
        }
    }

    // -------------------------------------------------
    // LOGGING HELPERS (sample-style)
    // -------------------------------------------------
    private IDisposable BeginSagaScope<T>(BehaviorContext<TransactionOrchestrationState, T> ctx)
        where T : class
    {
        var cid = string.IsNullOrWhiteSpace(ctx.Saga.CorrelationKey)
            ? GetCorrelationId(ctx, null)
            : ctx.Saga.CorrelationKey;

        var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["correlation_id"] = cid,
            ["transaction_id"] = ctx.Saga.TransactionId,
            ["state"] = ctx.Saga.CurrentState
        });
        
        return scope ?? NullDisposable.Instance;
    }

    private void LogTransition(string from, string to)
    {
        logger.LogInformation("Saga transition | {From} -> {To}", from, to);
    }
}
