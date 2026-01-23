namespace Support.Bot.Contracts;

public sealed record SupportTimelineItem(
    string EventType,
    string DisplayMessage,
    string? DetailsJson,
    DateTime OccurredAtUtc,
    string? Source
);