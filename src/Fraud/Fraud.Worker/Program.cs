using BuildingBlocks.Observability;
using Fraud.Worker.AI;
using Fraud.Worker.Consumers;
using Fraud.Worker.Health;
using Fraud.Worker.Rules;
using MassTransit;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

// Fraud Detection Rules
builder.Services.AddScoped<IFraudDetectionRule, HighAmountRule>();
builder.Services.AddScoped<IFraudDetectionRule, MerchantRiskRule>();
builder.Services.AddScoped<IFraudDetectionRule, GeographicRiskRule>();
builder.Services.AddScoped<FraudDetectionEngine>();

builder.Services.AddScoped<FallbackFraudExplanationGenerator>();

// Environment'dan API Key'i al
var openAiApiKey = builder.Configuration["OpenAi:ApiKey"] 
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (!string.IsNullOrWhiteSpace(openAiApiKey))
{
    Environment.SetEnvironmentVariable("OPENAI_API_KEY", openAiApiKey);
    builder.Services.AddScoped<IFraudExplanationGenerator, OpenAiFraudExplanationGenerator>();
}
else
{
    builder.Services.AddScoped<IFraudExplanationGenerator, LlmFraudExplanationGenerator>();
}

builder.Services.Configure<FraudExplanationOptions>(
    builder.Configuration.GetSection("FraudExplanation"));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<FraudCheckRequestedConsumer>();

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
               
        cfg.ReceiveEndpoint("fraud.fraud-check-requested", e =>
        {
            e.ConfigureConsumer<FraudCheckRequestedConsumer>(context);
        });

        cfg.UseConsumeFilter(typeof(CorrelationConsumeFilter<>), context);
        cfg.UsePublishFilter(typeof(CorrelationPublishFilter<>), context);
    });
});

builder.Services.AddHealthChecks()
    .AddRabbitMQ(rabbitConnectionString: "amqp://admin:admin@localhost:5672", name: "rabbitmq");

builder.Services.AddHostedService<HealthEndpointHostedService>();

var host = builder.Build();
host.Run();
