using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using MediatR;
using Transaction.Api.Outbox;
using Transaction.Application.Abstractions;
using Transaction.Application.Transactions;
using Transaction.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapPost("/transactions", async (CreateTransactionRequest req, ISender sender, CancellationToken ct) =>
{
    var correlationId = Guid.NewGuid().ToString();

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

app.UseHttpsRedirection();

app.Run();

public sealed record CreateTransactionRequest(decimal Amount, string Currency, string MerchantId);

