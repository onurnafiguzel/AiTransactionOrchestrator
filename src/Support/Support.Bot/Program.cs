using BuildingBlocks.Observability;
using Serilog;
using StackExchange.Redis;
using Support.Bot.Caching;
using Support.Bot.Contracts;
using Support.Bot.Data;
using Support.Bot.Health;
using Support.Bot.Logic;
using System.Transactions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==================== REDIS CONFIGURATION ====================
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";
var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// ==================== CACHING SERVICES ====================
builder.Services.AddScoped<ISupportTransactionCacheService, RedisSupportTransactionCacheService>();

var cs = builder.Configuration.GetConnectionString("SupportDb")
         ?? "Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass";

builder.Services.AddSingleton(new SupportReadRepository(cs));

builder.Services.AddHostedService<HealthEndpointHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/support/transactions/{transactionId:guid}", async (
    Guid transactionId,
    SupportReadRepository repo,
    ISupportTransactionCacheService cache,
    CancellationToken ct) =>
{
    // Try cache first
    var cachedReport = await cache.GetSupportTransactionAsync<SupportTransactionReport>(transactionId, ct);
    if (cachedReport is not null)
    {
        return Results.Ok(cachedReport);
    }

    var tx = await repo.GetTransaction(transactionId, ct);
    if (tx is null)
        return Results.NotFound(new { transactionId });

    var saga = await repo.GetSaga(transactionId, ct);

    var (summary, reason) = SupportSummaryBuilder.Build(
        status: Enum.GetName(typeof(TransactionStatus), tx.Status), 
        decisionReason: tx.Decision_Reason,
        retryCount: saga?.retry_Count ?? 0,
        timedOutAtUtc: saga?.timed_out_at_utc);

    var timelineRows = await repo.GetTimeline(transactionId, limit: 50, ct);

    var timeline = timelineRows
        .Select(x => new SupportTimelineItem(
            EventType: x.Event_Type,
            DisplayMessage: TimelineDisplayMessageBuilder.Build(x.Event_Type, x.Details_Json),
            DetailsJson: x.Details_Json,
            OccurredAtUtc: x.Occurred_At_Utc,
            Source: x.Source))
        .ToList();

    var explanation = (string?)null;

    var report = new SupportTransactionReport(
        TransactionId: transactionId,
        Status: Enum.GetName(typeof(TransactionStatus), tx.Status),
        Reason: tx.Decision_Reason ?? reason,
        Summary: summary,
        Explanation: tx.Explanation,
        Saga: new SupportSagaInfo(
            CurrentState: saga?.CurrentState,
            RetryCount: saga?.retry_Count ?? 0,
            TimedOutAtUtc: saga?.timed_out_at_utc,
            CorrelationId: saga?.CorrelationKey),
         Timeline: timeline,
        GeneratedAtUtc: DateTime.UtcNow);

    // Cache the report (10 minutes TTL)
    await cache.SetSupportTransactionAsync(transactionId, report, 10, ct);

    return Results.Ok(report);
});

app.MapGet("/support/incidents/summary", async (
    int? minutes,
    SupportReadRepository repo,
    ISupportTransactionCacheService cache,
    CancellationToken ct) =>
{
    var windowMinutes = Math.Clamp(minutes ?? 15, 1, 24 * 60);
    var cacheKey = $"incidents:summary:{windowMinutes}";

    // Try cache first (30 minutes TTL for incident summaries)
    var cachedSummary = await cache.GetIncidentSummaryAsync<IncidentSummary>(cacheKey, ct);
    if (cachedSummary is not null)
    {
        return Results.Ok(cachedSummary);
    }

    var toUtc = DateTime.UtcNow;
    var fromUtc = toUtc.AddMinutes(-windowMinutes);

    var counts = await repo.GetIncidentCounts(fromUtc, toUtc, ct);

    IReadOnlyList<MerchantTimeoutStat> topMerchants = Array.Empty<MerchantTimeoutStat>();
    try
    {
        var rows = await repo.GetTopMerchantsByTimedOut(fromUtc, toUtc, limit: 5, ct);
        topMerchants = rows.Select(x => new MerchantTimeoutStat(x.MerchantId, x.TimedOutCount)).ToList();
    }
    catch
    {
        topMerchants = Array.Empty<MerchantTimeoutStat>();
    }

    var timeoutRate = counts.Total == 0
        ? 0m
        : Math.Round((decimal)counts.TimedOut / counts.Total, 4);

    var summary = new IncidentSummary(
        WindowMinutes: windowMinutes,
        FromUtc: fromUtc,
        ToUtc: toUtc,
        TotalTransactions: counts.Total,
        ApprovedCount: counts.Approved,
        RejectedCount: counts.Rejected,
        TimedOutCount: counts.TimedOut,
        TimeoutRate: timeoutRate,
        TopMerchantsByTimeout: topMerchants);

    // Cache the summary (30 minutes TTL)
    await cache.SetIncidentSummaryAsync(cacheKey, summary, 30, ct);

    return Results.Ok(summary);
});

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.UseHttpsRedirection();

app.Run();
