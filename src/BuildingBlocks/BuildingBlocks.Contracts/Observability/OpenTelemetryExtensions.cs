using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter;
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
    /// Adds OpenTelemetry with Prometheus metrics exporter and Jaeger distributed tracing for HTTP services (APIs)
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

        var jaegerEnabled = builder.Configuration.GetValue<bool>("Tracing:Jaeger:Enabled", true);
        var jaegerHost = builder.Configuration["Tracing:Jaeger:Host"] ?? "localhost";
        var jaegerPort = builder.Configuration.GetValue<int>("Tracing:Jaeger:Port", 4317);
        var otlpEndpoint = builder.Configuration["Tracing:Otlp:Endpoint"]
            ?? $"http://{jaegerHost}:{jaegerPort}";

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
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddConsoleExporter();

                // Add OTLP exporter (using gRPC protocol - standard OTLP)
                if (jaegerEnabled)
                {
                    tracingBuilder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
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
    /// Adds OpenTelemetry with Jaeger distributed tracing for Worker services
    /// </summary>
    public static IHostBuilder AddOpenTelemetryWorker(
        this IHostBuilder builder,
        string serviceName,
        IConfiguration configuration)
    {
        return builder.ConfigureServices((context, services) =>
        {
            ConfigureWorkerOpenTelemetry(
                services,
                configuration,
                context.HostingEnvironment.EnvironmentName,
                serviceName);
        });
    }

    /// <summary>
    /// Adds OpenTelemetry with Jaeger distributed tracing for Worker services (HostApplicationBuilder)
    /// </summary>
    public static HostApplicationBuilder AddOpenTelemetryWorker(
        this HostApplicationBuilder builder,
        string serviceName)
    {
        ConfigureWorkerOpenTelemetry(
            builder.Services,
            builder.Configuration,
            builder.Environment.EnvironmentName,
            serviceName);

        return builder;
    }

    private static void ConfigureWorkerOpenTelemetry(
        IServiceCollection services,
        IConfiguration configuration,
        string environmentName,
        string serviceName)
    {
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["environment"] = environmentName,
                ["service.namespace"] = "aitransaction",
                ["service.version"] = "1.0.0"
            });

        var jaegerEnabled = configuration.GetValue<bool>("Tracing:Jaeger:Enabled", true);
        var jaegerHost = configuration["Tracing:Jaeger:Host"] ?? "localhost";
        var jaegerPort = configuration.GetValue<int>("Tracing:Jaeger:Port", 4317);
        var otlpEndpoint = configuration["Tracing:Otlp:Endpoint"]
            ?? $"http://{jaegerHost}:{jaegerPort}";
        var healthPort = configuration.GetValue<int>("Health:Port", 8080);
        var prometheusEnabled = configuration.GetValue<bool>("Metrics:Prometheus:Enabled", true);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracingBuilder =>
            {
                tracingBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddConsoleExporter();

                // Add OTLP exporter (using gRPC protocol - standard OTLP)
                if (jaegerEnabled)
                {
                    tracingBuilder.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
                }
            })
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (prometheusEnabled)
                {
                    metricsBuilder.AddPrometheusHttpListener(options =>
                        options.UriPrefixes = new[] { $"http://+:{healthPort}/" });
                }
            });
    }

    /// <summary>
    /// Adds MassTransit instrumentation for distributed tracing
    /// </summary>
    public static IServiceCollection AddMassTransitInstrumentation(
        this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracingBuilder =>
            {
                // MassTransit automatically adds its own instrumentation when detected
                // This ensures the tracing pipeline is ready
            });

        return services;
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
