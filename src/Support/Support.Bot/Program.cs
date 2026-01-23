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

app.Run();
