using Fraud.Worker.AI;
using Fraud.Worker.Consumers;
using MassTransit;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();

builder.Services.AddSerilog((sp, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext());

builder.Services.AddScoped<FallbackFraudExplanationGenerator>();

//builder.Services.AddScoped<IFraudExplanationGenerator>(sp =>
//{
//    return sp.GetRequiredService<OpenAiFraudExplanationGenerator>();
//});

builder.Services.AddScoped<IFraudExplanationGenerator, LlmFraudExplanationGenerator>();

builder.Services.Configure<FraudExplanationOptions>(
    builder.Configuration.GetSection("FraudExplanation"));

builder.Services.AddHttpClient<OpenAiFraudExplanationGenerator>(c =>
{
    c.Timeout = Timeout.InfiniteTimeSpan; // timeout'u biz CTS ile yönetiyoruz
});

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
    });
});

var host = builder.Build();
host.Run();
