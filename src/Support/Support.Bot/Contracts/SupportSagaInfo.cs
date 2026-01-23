namespace Support.Bot.Contracts;

public sealed record SupportSagaInfo(
    string? CurrentState,
    int RetryCount,
    DateTime? TimedOutAtUtc,
    string? CorrelationId
);
