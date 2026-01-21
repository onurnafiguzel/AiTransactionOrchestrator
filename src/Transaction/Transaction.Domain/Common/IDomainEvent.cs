namespace Transaction.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredAtUtc { get; }
}
