using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Transaction.Updater.Worker.Health;

public sealed class HealthEndpointHostedService(IConfiguration configuration) : IHostedService
{
    private IHost? _webHost;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var port = configuration.GetValue("Health:Port", 5101);

        _webHost = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web =>
            {
                web.UseUrls($"http://localhost:{port}");

                web.ConfigureServices(services =>
                {
                    var hc = services.AddHealthChecks();

                    var rabbit = configuration.GetValue<string>("Health:RabbitMq")
                                 ?? "amqp://admin:admin@localhost:5672";

                    hc.AddRabbitMQ(rabbitConnectionString: rabbit, name: "rabbitmq");

                    var pg = configuration.GetValue<string?>("Health:Postgres");
                    if (!string.IsNullOrWhiteSpace(pg))
                        hc.AddNpgSql(connectionString: pg, name: "postgres");
                });

                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHealthChecks("/health/live");
                        endpoints.MapHealthChecks("/health/ready");
                    });
                });
            })
            .Build();

        return _webHost.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => _webHost?.StopAsync(cancellationToken) ?? Task.CompletedTask;
}
