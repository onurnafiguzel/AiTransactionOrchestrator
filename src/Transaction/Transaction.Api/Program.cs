using BuildingBlocks.Observability;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Text;
using Transaction.Api.Middleware;
using Transaction.Api.Outbox;
using Transaction.Application;
using Transaction.Application.IP;
using Asp.Versioning;
using Transaction.Infrastructure;
using Transaction.Infrastructure.Caching;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

// Add OpenTelemetry instrumentation
builder.AddOpenTelemetryHttp("Transaction.Api");

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Transaction API",
        Version = "v1",
        Description = "AI Transaction Orchestrator - Transaction API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "your-256-bit-secret-key-min-32chars-change-this-in-production!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AiTransactionOrchestrator";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AiTransactionOrchestrator";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Customer", policy => policy.RequireRole("Customer", "Admin"));
});

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var multiplexer = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

builder.Services.AddScoped<ITransactionCacheService, RedisTransactionCacheService>();
builder.Services.AddScoped<IpAddressContext>();
builder.Services.AddTransactionInfrastructure(builder.Configuration.GetConnectionString("TransactionDb")!);
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

try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<Transaction.Infrastructure.Persistence.TransactionDbContext>();
        dbContext.Database.Migrate();
        app.Logger.LogInformation("Database migrations applied");
    }
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database migration failed");
    throw;
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<IpAddressMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.ConfigObject.AdditionalItems["persistAuthorization"] = true;
    });
}

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapPrometheusMetrics();
app.UseHttpsRedirection();
app.Run();
