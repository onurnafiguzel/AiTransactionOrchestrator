using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Contracts.Resiliency;

/// <summary>
/// Extension methods for registering resilience services.
/// </summary>
public static class ResilienceServiceExtensions
{
    /// <summary>
    /// Add resilience pipeline factory to DI container.
    /// </summary>
    public static IServiceCollection AddResiliencePipelines(this IServiceCollection services)
    {
        services.AddSingleton<ResiliencePipelineFactory>();
        return services;
    }
}
