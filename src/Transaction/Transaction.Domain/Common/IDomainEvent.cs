using MediatR;

namespace Transaction.Domain.Common;

public interface IDomainEvent : INotification
{
    DateTime OccurredAtUtc { get; }
}
