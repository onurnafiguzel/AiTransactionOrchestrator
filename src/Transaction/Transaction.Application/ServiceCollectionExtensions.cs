using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Transaction.Application.Behaviors;
using Transaction.Application.Transactions;

namespace Transaction.Application;

/// <summary>
/// Extension methods for registering Application layer services.
/// Configures MediatR with validation pipeline and command handlers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Application layer services including MediatR with validation behavior.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add validators - validation happens in ValidationBehavior pipeline
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        // Register MediatR with handlers and behaviors
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
            
            // Pipeline behaviors - order matters (outermost to innermost)
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));      // 1. Log before/after
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));   // 2. Validate input
            config.AddOpenBehavior(typeof(TransactionBehavior<,>));  // 3. DB transaction
        });

        return services;
    }
}
