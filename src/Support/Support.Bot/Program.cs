using Support.Bot.Contracts;
using Support.Bot.Data;
using Support.Bot.Logic;
using System.Transactions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var cs = builder.Configuration.GetConnectionString("SupportDb")
         ?? "Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass";

builder.Services.AddSingleton(new SupportReadRepository(cs));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/support/transactions/{transactionId:guid}", async (
    Guid transactionId,
    SupportReadRepository repo,
    CancellationToken ct) =>
{
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

    return Results.Ok(report);
});

app.MapGet("/support/incidents/summary", async (
    int? minutes,
    SupportReadRepository repo,
    CancellationToken ct) =>
{
    var windowMinutes = Math.Clamp(minutes ?? 15, 1, 24 * 60);

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

    return Results.Ok(new IncidentSummary(
        WindowMinutes: windowMinutes,
        FromUtc: fromUtc,
        ToUtc: toUtc,
        TotalTransactions: counts.Total,
        ApprovedCount: counts.Approved,
        RejectedCount: counts.Rejected,
        TimedOutCount: counts.TimedOut,
        TimeoutRate: timeoutRate,
        TopMerchantsByTimeout: topMerchants));
});
app.Run();
