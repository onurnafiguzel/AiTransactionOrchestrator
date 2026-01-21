using Transaction.Domain.Common;

namespace Transaction.Domain.Transactions.Events;

public sealed record TransactionDeletedDomainEvent(
    Guid TransactionId
) : DomainEvent;
