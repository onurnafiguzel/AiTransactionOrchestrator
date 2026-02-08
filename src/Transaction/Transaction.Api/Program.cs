using BuildingBlocks.Contracts.Observability;
using BuildingBlocks.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;
using Transaction.Api.Middleware;
using Transaction.Api.Outbox;
using Transaction.Application;
using Transaction.Application.IP;
using Transaction.Infrastructure;
using Transaction.Infrastructure.Caching;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Controllers
builder.Services.AddControllers();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ==================== REDIS CONFIGURATION ====================
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";
var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// ==================== CACHING SERVICES ====================
builder.Services.AddScoped<ITransactionCacheService, RedisTransactionCacheService>();

// IP Address context for fraud detection
builder.Services.AddScoped<IpAddressContext>();

builder.Services.AddTransactionInfrastructure(
    builder.Configuration.GetConnectionString("TransactionDb")!);

// Add Application services with MediatR, validation, and handlers
builder.Services.AddApplicationServices();

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

        cfg.UseConsumeFilter(typeof(CorrelationConsumeFilter<>), context);
        cfg.UsePublishFilter(typeof(CorrelationPublishFilter<>), context);
    });
});

builder.Services.AddHostedService<OutboxPublisherService>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("TransactionDb")!, name: "postgres")
    .AddRedis(redisConnectionString, name: "redis")
    .AddRabbitMQ(rabbitConnectionString: "amqp://admin:admin@rabbitmq:5672", name: "rabbitmq");


var app = builder.Build();

// Apply database migrations automatically
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<Transaction.Infrastructure.Persistence.TransactionDbContext>();
        dbContext.Database.Migrate();
        app.Logger.LogInformation("✅ Database migrations applied successfully");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "❌ Database migration failed");
    throw;
}

// Correlation ID tracking
app.UseMiddleware<CorrelationIdMiddleware>();

// IP Address extraction
app.UseMiddleware<IpAddressMiddleware>();

// Request/Response logging
app.UseMiddleware<RequestResponseLoggingMiddleware>();

// Exception handling
app.UseMiddleware<ExceptionHandlerMiddleware>();

// Use CORS policy
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map controller routes
app.MapControllers();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.UseHttpsRedirection();

app.Run();

