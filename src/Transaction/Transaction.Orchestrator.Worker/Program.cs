using BuildingBlocks.Contracts.Resiliency;
using BuildingBlocks.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using Transaction.Orchestrator.Worker.Persistence;
using Transaction.Orchestrator.Worker.Saga;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

// Add OpenTelemetry instrumentation with distributed tracing
builder.AddOpenTelemetryWorker("Transaction.Orchestrator");
builder.Services.AddMassTransitInstrumentation();

// Add Resilience Pipelines
builder.Services.AddResiliencePipelines();

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMq:Username"] ?? "admin";
var rabbitPass = builder.Configuration["RabbitMq:Password"] ?? "admin";

var sagaCs = builder.Configuration.GetConnectionString("SagaDb")
            ?? "Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass";

builder.Services.AddSingleton(new Transaction.Orchestrator.Worker.Timeline.TimelineWriter(sagaCs));

builder.Services.AddDbContext<OrchestratorSagaDbContext>(opt =>
{
    opt.UseNpgsql(sagaCs, npgsqlOptions =>
    {
        // Enable automatic retry on transient failures
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);

        // Command timeout
        npgsqlOptions.CommandTimeout(30);
    });
});

builder.Services.AddMassTransit(x =>
{
    // Quartz scheduler consumer'ları
    x.AddQuartzConsumers();

    x.AddSagaStateMachine<TransactionOrchestrationStateMachine, TransactionOrchestrationState>()
        .Endpoint(e => e.Name = "transaction-orchestrator")
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;

            r.AddDbContext<DbContext, OrchestratorSagaDbContext>((_, cfg) => cfg.UseNpgsql(sagaCs));
        });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // Quartz scheduler address
        cfg.UseMessageScheduler(new Uri("queue:quartz"));
        // Quartz endpoint: scheduled mesajlar burada işlenir
        cfg.ReceiveEndpoint("quartz", e =>
        {
            // Configure message retry with fixed interval (5 retries, 5 seconds between retries)
            // If all retries fail, message automatically goes to quartz-error queue
            e.UseMessageRetry(r =>
            {
                r.Interval(5, TimeSpan.FromSeconds(5));
            });

            e.ConfigureQuartzConsumers(context);
        });

        cfg.ConfigureEndpoints(context);

        cfg.UseConsumeFilter(typeof(CorrelationConsumeFilter<>), context);
        cfg.UsePublishFilter(typeof(CorrelationPublishFilter<>), context);
    });
});

builder.Services.AddQuartz();

builder.Services.AddQuartzHostedService();

// HealthEndpointHostedService temporarily disabled - using PrometheusMetricsHostedService for /metrics
// builder.Services.AddHostedService<HealthEndpointHostedService>();

var host = builder.Build();

// Apply database migrations automatically
try
{
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<OrchestratorSagaDbContext>();
        dbContext.Database.Migrate();
        host.Services.GetRequiredService<Serilog.ILogger>()
            .Information("✅ Saga database migrations applied successfully");
    }
}
catch (Exception ex)
{
    host.Services.GetRequiredService<Serilog.ILogger>()
        .Error(ex, "❌ Saga database migration failed");
    throw;
}

host.Run();
