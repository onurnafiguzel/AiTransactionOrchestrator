using BuildingBlocks.Observability;
using Serilog;
using StackExchange.Redis;
using Support.Bot.Caching;
using Support.Bot.Data;
using Support.Bot.Health;

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
builder.Services.AddScoped<ISupportTransactionCacheService, RedisSupportTransactionCacheService>();

var cs = builder.Configuration.GetConnectionString("SupportDb")
         ?? "Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass";

builder.Services.AddSingleton(new SupportReadRepository(cs));

builder.Services.AddHostedService<HealthEndpointHostedService>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("SupportDb")!)
    .AddRabbitMQ(rabbitConnectionString: "amqp://admin:admin@rabbitmq:5672");

var app = builder.Build();

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
