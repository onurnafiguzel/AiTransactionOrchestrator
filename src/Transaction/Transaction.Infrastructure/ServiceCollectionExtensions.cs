using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Transaction.Application.Abstractions;
using Transaction.Application.Outbox;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Outbox;
using Transaction.Infrastructure.Persistence;
using Transaction.Infrastructure.Repositories;

namespace Transaction.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<TransactionDbContext>(opt =>
            opt.UseNpgsql(connectionString));

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IOutboxWriter, EfCoreOutboxWriter>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<InboxGuard>();


        return services;
    }
}
