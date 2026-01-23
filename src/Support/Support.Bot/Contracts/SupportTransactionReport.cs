namespace Support.Bot.Contracts;

public sealed record SupportTransactionReport(
    Guid TransactionId,
    string Status,
    string? Reason,
    string Summary,
    string? Explanation,
    SupportSagaInfo Saga,
    IReadOnlyList<SupportTimelineItem> Timeline,
    DateTime GeneratedAtUtc
);