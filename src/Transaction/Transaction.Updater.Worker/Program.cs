using BuildingBlocks.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;
using Transaction.Infrastructure;
using Transaction.Infrastructure.Caching;
using Transaction.Updater.Worker.Consumers;
using Transaction.Updater.Worker.Health;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

// Add OpenTelemetry instrumentation
var resourceBuilder = ResourceBuilder.CreateDefault().AddService("Transaction.Updater");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Transaction.Updater"))
    .WithTracing(tracingBuilder =>
    {
        tracingBuilder
            .SetResourceBuilder(resourceBuilder)
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation();
    })
    .WithMetrics(metricsBuilder =>
    {
        metricsBuilder
            .SetResourceBuilder(resourceBuilder)
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusHttpListener(options => options.UriPrefixes = new string[] { $"http://+:5030/" });
    });

var cs = builder.Configuration.GetConnectionString("TransactionDb")
         ?? "Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass";

builder.Services.AddTransactionInfrastructure(cs);

builder.Services.AddSingleton(new Transaction.Updater.Worker.Timeline.TimelineWriter(cs));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TransactionApprovedConsumer>();
    x.AddConsumer<TransactionRejectedConsumer>();

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

        cfg.ReceiveEndpoint("transaction.outcomes", e =>
        {
            e.ConfigureConsumer<TransactionApprovedConsumer>(context);
            e.ConfigureConsumer<TransactionRejectedConsumer>(context);
        });

        cfg.UseConsumeFilter(typeof(CorrelationConsumeFilter<>), context);
        cfg.UsePublishFilter(typeof(CorrelationPublishFilter<>), context);
    });
});

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

builder.Services.AddScoped<ITransactionCacheService, RedisTransactionCacheService>();
// HealthEndpointHostedService temporarily disabled - using PrometheusMetricsHostedService for /metrics
// builder.Services.AddHostedService<HealthEndpointHostedService>();

var host = builder.Build();

// Apply database migrations automatically
try
{
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<Transaction.Infrastructure.Persistence.TransactionDbContext>();
        dbContext.Database.Migrate();
        host.Services.GetRequiredService<Serilog.ILogger>()
            .Information("✅ Transaction database migrations applied successfully");
    }
}
catch (Exception ex)
{
    host.Services.GetRequiredService<Serilog.ILogger>()
        .Error(ex, "❌ Transaction database migration failed");
    throw;
}

host.Run();
