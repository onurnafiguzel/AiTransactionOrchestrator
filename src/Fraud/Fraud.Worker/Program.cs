using BuildingBlocks.Contracts.Resiliency;
using BuildingBlocks.Observability;
using Fraud.Worker.AI;
using Fraud.Worker.Caching;
using Fraud.Worker.Consumers;
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

// Add OpenTelemetry instrumentation with distributed tracing
builder.AddOpenTelemetryWorker("Fraud.Worker");
builder.Services.AddMassTransitInstrumentation();

// ==================== RESILIENCE PIPELINES ====================
builder.Services.AddResiliencePipelines();

// ==================== REDIS CONFIGURATION ====================
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379";
var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// ==================== REDIS CACHE SERVICES ====================
// Global merchant and geographic risk caches
builder.Services.AddSingleton<IMerchantRiskCacheService, RedisMerchantRiskCacheService>();
builder.Services.AddSingleton<IGeographicRiskCacheService, RedisGeographicRiskCacheService>();
builder.Services.AddHostedService<RedisCacheSeederHostedService>();

// User-specific caches
builder.Services.AddSingleton<IUserThresholdCacheService, RedisUserThresholdCacheService>();
builder.Services.AddSingleton<IUserMerchantRestrictionCacheService, RedisUserMerchantRestrictionCacheService>();
builder.Services.AddSingleton<IUserGeographicRestrictionCacheService, RedisUserGeographicRestrictionCacheService>();
builder.Services.AddSingleton<IUserDailySpendingCacheService, RedisUserDailySpendingCacheService>();

// Velocity Check Service (Redis-backed for production)
builder.Services.AddSingleton<IVelocityCheckService, RedisVelocityCheckService>();
builder.Services.AddHostedService<VelocityCheckCleanupHostedService>();

// Fraud Detection Rules
// Amount-based rules
builder.Services.AddScoped<IFraudDetectionRule>(sp =>
    new HighAmountRule(sp.GetRequiredService<IUserThresholdCacheService>()));
builder.Services.AddScoped<IFraudDetectionRule>(sp =>
    new UserDailyLimitRule(
        sp.GetRequiredService<IUserDailySpendingCacheService>(),
        sp.GetRequiredService<ILogger<UserDailyLimitRule>>()));

// Merchant-based rules
builder.Services.AddScoped<IFraudDetectionRule>(sp =>
    new MerchantRiskRule(
        sp.GetRequiredService<IMerchantRiskCacheService>(),
        sp.GetRequiredService<IUserMerchantRestrictionCacheService>(),
        sp.GetRequiredService<ILogger<MerchantRiskRule>>()));

// Geographic-based rules
builder.Services.AddScoped<IFraudDetectionRule>(sp =>
    new GeographicRiskRule(
        sp.GetRequiredService<IGeographicRiskCacheService>(),
        sp.GetRequiredService<IUserGeographicRestrictionCacheService>(),
        sp.GetRequiredService<ILogger<GeographicRiskRule>>()));
builder.Services.AddScoped<IFraudDetectionRule>(sp =>
    new UserLocationAnomalyRule(
        sp.GetRequiredService<IUserGeographicRestrictionCacheService>(),
        sp.GetRequiredService<ILogger<UserLocationAnomalyRule>>()));

// User-based rules
builder.Services.AddScoped<IFraudDetectionRule>(sp =>
    new UserRiskRule(sp.GetRequiredService<ILogger<UserRiskRule>>()));

// Velocity-based rules (userId powered)
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
            // Configure message retry with fixed interval (5 retries, 5 seconds between retries)
            // If all retries fail, message automatically goes to fraud.fraud-check-requested-error queue
            e.UseMessageRetry(r =>
            {
                r.Interval(5, TimeSpan.FromSeconds(5));
            });

            e.ConfigureConsumer<FraudCheckRequestedConsumer>(context);
        });

        cfg.UseConsumeFilter(typeof(CorrelationConsumeFilter<>), context);
        cfg.UsePublishFilter(typeof(CorrelationPublishFilter<>), context);
    });
});

builder.Services.AddHealthChecks()
    .AddRedis(redisConnectionString, name: "redis")
    .AddRabbitMQ(rabbitConnectionString: "amqp://admin:admin@localhost:5672", name: "rabbitmq");

// HealthEndpointHostedService temporarily disabled - using PrometheusMetricsHostedService for /metrics
// builder.Services.AddHostedService<HealthEndpointHostedService>();

var host = builder.Build();
host.Run();
