using MassTransit;
using Transaction.Infrastructure;
using Transaction.Updater.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

var cs = builder.Configuration.GetConnectionString("TransactionDb")
         ?? "Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass";

builder.Services.AddTransactionInfrastructure(cs);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TransactionApprovedConsumer>();
    x.AddConsumer<TransactionRejectedConsumer>();

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

        cfg.ReceiveEndpoint("transaction.outcomes", e =>
        {
            e.ConfigureConsumer<TransactionApprovedConsumer>(context);
            e.ConfigureConsumer<TransactionRejectedConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();
