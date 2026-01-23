using MassTransit;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Transaction.Orchestrator.Worker.Persistence;
using Transaction.Orchestrator.Worker.Saga;

var builder = Host.CreateApplicationBuilder(args);

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMq:Username"] ?? "admin";
var rabbitPass = builder.Configuration["RabbitMq:Password"] ?? "admin";

var sagaCs = builder.Configuration.GetConnectionString("SagaDb")
            ?? "Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass";

builder.Services.AddSingleton(new Transaction.Orchestrator.Worker.Timeline.TimelineWriter(sagaCs));

builder.Services.AddDbContext<OrchestratorSagaDbContext>(opt =>
{
    opt.UseNpgsql(sagaCs);
});

builder.Services.AddMassTransit(x =>
{
    // Quartz scheduler consumer'ları
    x.AddQuartzConsumers();

    x.AddSagaStateMachine<TransactionOrchestrationStateMachine, TransactionOrchestrationState>()
        .Endpoint(e=>e.Name = "transaction-orchestrator")
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
            e.ConfigureQuartzConsumers(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddQuartz();

builder.Services.AddQuartzHostedService();

var host = builder.Build();
host.Run();
