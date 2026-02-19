using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability;

/// <summary>
/// Extension methods for OpenTelemetry configuration
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry with Prometheus metrics exporter for HTTP services (APIs)
    /// </summary>
    public static WebApplicationBuilder AddOpenTelemetryHttp(
        this WebApplicationBuilder builder,
        string serviceName)
    {
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["environment"] = builder.Environment.EnvironmentName,
                ["service.namespace"] = "aitransaction",
                ["service.version"] = "1.0.0"
            });

        // Add Tracing
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracingBuilder =>
            {
                tracingBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.FilterHttpRequestMessage = ctx => !ctx.RequestUri?.AbsolutePath.Contains("/health") == true;
                    })
                    .AddSqlClientInstrumentation()
                    .AddConsoleExporter();
            })
            // Add Metrics
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();
            });

        return builder;
    }

    /// <summary>
    /// Maps Prometheus metrics endpoint
    /// </summary>
    public static WebApplication MapPrometheusMetrics(this WebApplication app)
    {
        app.MapPrometheusScrapingEndpoint();
        return app;
    }
}
