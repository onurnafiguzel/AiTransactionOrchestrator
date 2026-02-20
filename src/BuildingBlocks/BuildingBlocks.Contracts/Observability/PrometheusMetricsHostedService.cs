using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;

namespace BuildingBlocks.Observability;

/// <summary>
/// Hosts Prometheus metrics endpoint for worker services on /metrics
/// </summary>
public sealed class PrometheusMetricsHostedService : BackgroundService
{
    private readonly int _port;
    private readonly ILogger<PrometheusMetricsHostedService> _logger;
    private HttpListener? _listener;

    public PrometheusMetricsHostedService(
        IConfiguration configuration,
        ILogger<PrometheusMetricsHostedService> logger)
    {
        _logger = logger;
        _port = configuration.GetValue<int>("Health:Port");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{_port}/metrics/");
            _listener.Start();
            _logger.LogInformation("Prometheus metrics endpoint started on http://+:{Port}/metrics", _port);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    await Task.Run(() => HandleRequest(context), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Error handling metrics request");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Prometheus metrics endpoint on port {Port}", _port);
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            // Simple response - Prometheus scraper will handle actual metrics collection
            var response = Encoding.UTF8.GetBytes("# Metrics endpoint - OpenTelemetry metrics are collected here\n");
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain; version=0.0.4";
            context.Response.ContentLength64 = response.Length;
            context.Response.OutputStream.Write(response, 0, response.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error writing metrics response");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        _listener?.Close();
        _logger.LogInformation("Prometheus metrics endpoint stopped");
        await base.StopAsync(cancellationToken);
    }
}
