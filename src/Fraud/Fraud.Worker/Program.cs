using BuildingBlocks.Observability;
using Fraud.Worker.AI;
using Fraud.Worker.Caching;
using Fraud.Worker.Consumers;
using Fraud.Worker.Health;
using Fraud.Worker.Policies;
using Fraud.Worker.Rules;
using Fraud.Worker.VelocityCheck;
using MassTransit;
using Serilog;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

// ==================== REDIS CONFIGURATION ====================
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";
var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// ==================== REDIS CACHE SERVICES ====================
builder.Services.AddSingleton<IMerchantRiskCacheService, RedisMerchantRiskCacheService>();
builder.Services.AddSingleton<IGeographicRiskCacheService, RedisGeographicRiskCacheService>();
builder.Services.AddHostedService<RedisCacheSeederHostedService>();

// Velocity Check Service (Redis-backed for production)
builder.Services.AddSingleton<IVelocityCheckService, RedisVelocityCheckService>();
builder.Services.AddHostedService<VelocityCheckCleanupHostedService>();

// Fraud Detection Rules
builder.Services.AddScoped<IFraudDetectionRule, HighAmountRule>();
builder.Services.AddScoped<IFraudDetectionRule, MerchantRiskRule>();
builder.Services.AddScoped<IFraudDetectionRule, GeographicRiskRule>();
builder.Services.AddScoped<IFraudDetectionRule>(sp => 
    new VelocityCheckRule(sp.GetRequiredService<IVelocityCheckService>()));

// Circuit Breaker Policy for Fraud Detection
builder.Services.AddSingleton<FraudCheckCircuitBreakerPolicy>();

// Fraud Engine (with circuit breaker protection)
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
