namespace Transaction.Domain.Common;

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
