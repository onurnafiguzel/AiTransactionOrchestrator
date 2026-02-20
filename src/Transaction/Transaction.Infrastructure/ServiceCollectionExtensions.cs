using BuildingBlocks.Contracts.Resiliency;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Transaction.Application.Abstractions;
using Transaction.Application.Outbox;
using Transaction.Application.Users;
using Transaction.Infrastructure.Authentication;
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
        // Register DbContext with IMediator via factory and retry policy
        services.AddScoped(sp =>
        {
            var options = new DbContextOptionsBuilder<TransactionDbContext>()
                .UseNpgsql(connectionString, npgsqlOptions =>
                {
                    // Enable automatic retry on transient failures
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);

                    // Command timeout
                    npgsqlOptions.CommandTimeout(30);
                })
                .Options;

            var mediator = sp.GetService<IMediator>();
            return new TransactionDbContext(options, mediator);
        });

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IUserRepository, Transaction.Infrastructure.Persistence.Repositories.UserRepository>();
        services.AddScoped<IOutboxWriter, EfCoreOutboxWriter>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<InboxGuard>();

        // Authentication services
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        // Resilience pipelines for retry policies
        services.AddResiliencePipelines();

        return services;
    }
}
