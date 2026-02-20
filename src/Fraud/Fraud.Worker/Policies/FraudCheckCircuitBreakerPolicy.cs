using Fraud.Worker.Rules;
using Polly;
using Polly.CircuitBreaker;

namespace Fraud.Worker.Policies;

/// <summary>
/// Circuit breaker policy for fraud check operations.
/// Prevents cascade failures when too many timeouts/failures occur.
/// 
/// Policy behavior:
/// - CLOSED state (normal): Requests processed normally
/// - OPEN state (failures detected): All requests rejected immediately
/// - HALF_OPEN state (recovery testing): Single request allowed to test recovery
/// </summary>
public sealed class FraudCheckCircuitBreakerPolicy(
    ILogger<FraudCheckCircuitBreakerPolicy> logger)
{
    private readonly IAsyncPolicy<FraudRuleResult> _policy = CreatePolicy(logger);

    /// <summary>
    /// Executes fraud check rule with circuit breaker protection.
    /// </summary>
    public async Task<FraudRuleResult> ExecuteAsync(
        Func<CancellationToken, Task<FraudRuleResult>> action,
        CancellationToken ct = default)
    {
        try
        {
            return await _policy.ExecuteAsync((token) => action(token), ct);
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogError(
                "🔴 Fraud check circuit breaker OPEN. Request rejected immediately. " +
                "Circuit will retry in 60 seconds. Exception: {ExceptionMessage}",
                ex.Message);

            return new FraudRuleResult(
                RuleName: "CircuitBreaker",
                IsFraud: true,
                RiskScore: 100,
                Reason: "Circuit breaker open - service unavailable");
        }
    }

    private static IAsyncPolicy<FraudRuleResult> CreatePolicy(
        ILogger<FraudCheckCircuitBreakerPolicy> logger)
    {
        return Policy<FraudRuleResult>
            .Handle<TimeoutException>()
            .Or<OperationCanceledException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (outcome, duration) =>
                {
                    logger.LogWarning(
                        "🔴 Fraud check circuit breaker OPENED. Breaking for {DurationSeconds}s. " +
                        "Exception: {ExceptionMessage}",
                        (int)duration.TotalSeconds,
                        outcome.Exception?.Message ?? "Unknown error");
                },
                onReset: () =>
                {
                    logger.LogInformation("🟢 Fraud check circuit breaker RESET. Resuming normal operation.");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("🟡 Fraud check circuit breaker HALF-OPEN. Testing recovery...");
                });
    }
}