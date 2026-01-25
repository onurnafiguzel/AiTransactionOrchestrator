using BuildingBlocks.Contracts.Observability;
using BuildingBlocks.Observability;
using MassTransit;
using MediatR;
using Serilog;
using Transaction.Api.Middleware;
using Transaction.Api.Outbox;
using Transaction.Application.Abstractions;
using Transaction.Application.Transactions;
using Transaction.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

//builder.Host.UseSerilog((ctx, lc) =>
//{
//    lc.ReadFrom.Configuration(ctx.Configuration)
//      .Enrich.With<CorrelationIdEnricher>();
//});

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddTransactionInfrastructure(
    builder.Configuration.GetConnectionString("TransactionDb")!);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Transaction.Application.Transactions.CreateTransactionCommand).Assembly));

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var user = builder.Configuration["RabbitMq:Username"] ?? "admin";
        var pass = builder.Configuration["RabbitMq:Password"] ?? "admin";

        cfg.Host(host, h =>
        {
            h.Username(user);
            h.Password(pass);
        });
    });
});

builder.Services.AddHostedService<OutboxPublisherService>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("TransactionDb")!)
    .AddRabbitMQ(rabbitConnectionString: "amqp://admin:admin@localhost:5672");


var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapPost("/transactions", async (
    CreateTransactionRequest req,
    ISender sender,
    HttpContext http,
    CancellationToken ct) =>
{
    var correlationId =
        CorrelationContext.CorrelationId
        ?? (http.Request.Headers.TryGetValue(Correlation.HeaderName, out var v) ? v.ToString() : Guid.NewGuid().ToString("N"));

    var id = await sender.Send(
        new CreateTransactionCommand(req.Amount, req.Currency, req.MerchantId, correlationId),
        ct);

    return Results.Created($"/transactions/{id}", new { transactionId = id, correlationId });
});

app.MapGet("/transactions/{id:guid}", async (Guid id, ITransactionRepository repo, CancellationToken ct) =>
{
    var tx = await repo.Get(id, ct);
    return tx is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            tx.Id,
            tx.Amount,
            tx.Currency,
            tx.MerchantId,
            tx.Status,
            tx.CreatedAtUtc,
            tx.UpdatedAtUtc,
            tx.IsDeleted
        });
});

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.UseHttpsRedirection();

app.Run();

public sealed record CreateTransactionRequest(decimal Amount, string Currency, string MerchantId);

