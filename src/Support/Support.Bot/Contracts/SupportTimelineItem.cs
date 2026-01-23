namespace Support.Bot.Contracts;

public sealed record SupportTimelineItem(
    string EventType,
    string? DetailsJson,
    DateTime OccurredAtUtc,
    string? Source
);