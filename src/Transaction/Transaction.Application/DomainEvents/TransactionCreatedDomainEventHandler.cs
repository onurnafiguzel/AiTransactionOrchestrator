using MediatR;
using Transaction.Domain.Transactions.Events;

namespace Transaction.Application.DomainEvents;

public sealed class TransactionCreatedDomainEventHandler()
    : INotificationHandler<TransactionCreatedDomainEvent>
{
    public Task Handle(TransactionCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        // Implement email sending logic here for creating transaction
        return Task.CompletedTask;
    }
}
