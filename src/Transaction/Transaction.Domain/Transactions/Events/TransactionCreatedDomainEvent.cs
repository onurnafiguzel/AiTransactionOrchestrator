using Transaction.Domain.Common;

namespace Transaction.Domain.Transactions.Events;

public sealed record TransactionCreatedDomainEvent(
    Guid TransactionId,
    Guid UserId
) : DomainEvent;
