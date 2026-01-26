using MediatR;
using Transaction.Domain.Transactions.Events;

namespace Transaction.Application.DomainEvents;

public sealed class TransactionStatusDomainEventHandler()
    : INotificationHandler<TransactionStatusDomainEvent>
{
    public Task Handle(TransactionStatusDomainEvent notification, CancellationToken cancellationToken)
    {
        // Implement email sending logic here for transaction change status
        return Task.CompletedTask;
    }
}
