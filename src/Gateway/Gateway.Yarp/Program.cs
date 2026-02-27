using BuildingBlocks.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==================== LOGGING ====================
builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.With<CorrelationIdEnricher>());

// ==================== OPENTELEMETRY ====================
builder.AddOpenTelemetryHttp("Gateway.Yarp");

// ==================== YARP REVERSE PROXY ====================
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ==================== JWT AUTHENTICATION ====================
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? "your-256-bit-secret-key-min-32chars-change-this-in-production!";
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

// ==================== CORS ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ==================== RATE LIMITING ====================
builder.Services.AddRateLimiter(options =>
{
    // Fixed Window - Transaction Creation: 10 requests per minute PER USER
    options.AddPolicy("transaction-create", context =>
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst("sub")?.Value
                  ?? context.Connection.RemoteIpAddress?.ToString()
                  ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2
        });
    });

    // Sliding Window - Transaction Query: 100 requests per minute PER USER
    options.AddPolicy("transaction-query", context =>
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst("sub")?.Value
                  ?? context.Connection.RemoteIpAddress?.ToString()
                  ?? "anonymous";

        return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 5
        });
    });

    // Token Bucket - Auth endpoints: 5 requests per 10 seconds PER IP
    options.AddPolicy("auth", context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 5,
            ReplenishmentPeriod = TimeSpan.FromSeconds(10),
            TokensPerPeriod = 2,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 1
        });
    });

    // Sliding Window - Support API: 50 requests per minute PER USER
    options.AddPolicy("support", context =>
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst("sub")?.Value
                  ?? context.Connection.RemoteIpAddress?.ToString()
                  ?? "anonymous";

        return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 50,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 3
        });
    });

    // Concurrency Limiter - Global: Max 1000 concurrent requests
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = 1000,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 100
        });
    });

    // Default policy - 429 response
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        TimeSpan? retryAfter = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue))
        {
            retryAfter = retryAfterValue;
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfterValue.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests",
            message = "Rate limit exceeded. Please try again later.",
            retryAfterSeconds = retryAfter.HasValue ? (int)retryAfter.Value.TotalSeconds : (int?)null
        }, cancellationToken);
    };
});

// ==================== HEALTH CHECKS ====================
builder.Services.AddHealthChecks();

// ==================== REQUEST TIMEOUTS ====================
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    options.AddPolicy("short", TimeSpan.FromSeconds(10));
    options.AddPolicy("medium", TimeSpan.FromSeconds(30));
    options.AddPolicy("long", TimeSpan.FromSeconds(60));
});

// ==================== BUILD APP ====================
var app = builder.Build();

// ==================== MIDDLEWARE PIPELINE ====================
app.UseSerilogRequestLogging();
app.UseCors("AllowAll");
app.UseRouting();
app.UseRequestTimeouts();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapPrometheusMetrics();

app.Run();
