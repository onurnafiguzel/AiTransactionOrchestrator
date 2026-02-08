using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Support.Bot.Health;

/// <summary>
/// Embedded health check endpoint for support bot service.
/// Provides detailed JSON response with all dependency statuses.
/// Runs on separate port to avoid conflicts with main application.
/// </summary>
public sealed class HealthEndpointHostedService(IConfiguration configuration) : IHostedService
{
    private IHost? _webHost;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var port = configuration.GetValue("Health:Port", 5102);
        var rabbitMq = configuration.GetValue<string>("Health:RabbitMq") 
            ?? "amqp://admin:admin@localhost:5672";
        var redis = configuration.GetValue<string>("Health:Redis") 
            ?? "localhost:6379";
        var postgres = configuration.GetValue<string?>("Health:Postgres");

        _webHost = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web =>
            {
                web.UseUrls($"http://localhost:{port}");

                web.ConfigureServices(services =>
                {
                    var hc = services.AddHealthChecks();

                    // RabbitMQ health check
                    hc.AddRabbitMQ(rabbitMq, name: "rabbitmq");

                    // Redis health check
                    hc.AddRedis(redis, name: "redis");

                    // PostgreSQL health check (optional)
                    if (!string.IsNullOrWhiteSpace(postgres))
                    {
                        hc.AddNpgSql(connectionString: postgres, name: "postgres");
                    }
                });

                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        // 🎯 DETAILED HEALTH CHECK RESPONSE
                        endpoints.MapHealthChecks(
                            "/health/live",
                            new HealthCheckOptions
                            {
                                ResponseWriter = WriteDetailedHealthResponse,
                                Predicate = _ => true
                            });

                        endpoints.MapHealthChecks(
                            "/health/ready",
                            new HealthCheckOptions
                            {
                                ResponseWriter = WriteDetailedHealthResponse,
                                Predicate = _ => true
                            });

                        // 📊 SUMMARY ENDPOINT
                        endpoints.MapHealthChecks(
                            "/health",
                            new HealthCheckOptions
                            {
                                ResponseWriter = WriteDetailedHealthResponse,
                                Predicate = _ => true
                            });
                    });
                });
            })
            .Build();

        await _webHost.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => _webHost?.StopAsync(cancellationToken) ?? Task.CompletedTask;

    /// <summary>
    /// Writes detailed health check response as formatted JSON.
    /// </summary>
    private static Task WriteDetailedHealthResponse(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow.ToString("O"),
            overallHealthStatus = report.Status switch
            {
                HealthStatus.Healthy => "✅ All services healthy",
                HealthStatus.Degraded => "⚠️  Some services degraded",
                HealthStatus.Unhealthy => "❌ One or more services unhealthy",
                _ => "❓ Unknown status"
            },
            totalDuration = report.TotalDuration.TotalMilliseconds.ToString("F2") + "ms",
            
            // 📊 DETAILED CHECKS
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    statusIcon = entry.Value.Status switch
                    {
                        HealthStatus.Healthy => "✅",
                        HealthStatus.Degraded => "⚠️",
                        HealthStatus.Unhealthy => "❌",
                        _ => "❓"
                    },
                    durationMs = entry.Value.Duration.TotalMilliseconds.ToString("F2"),
                    description = entry.Value.Description,
                    exception = entry.Value.Exception?.Message,
                    data = entry.Value.Data?.Any() == true
                        ? entry.Value.Data
                        : null
                }),

            // 🔍 SUMMARY
            summary = new
            {
                totalChecks = report.Entries.Count,
                healthyChecks = report.Entries.Count(e => e.Value.Status == HealthStatus.Healthy),
                degradedChecks = report.Entries.Count(e => e.Value.Status == HealthStatus.Degraded),
                unhealthyChecks = report.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy)
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return context.Response.WriteAsJsonAsync(response, options);
    }
}
