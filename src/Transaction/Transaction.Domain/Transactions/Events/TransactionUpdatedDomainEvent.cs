using Transaction.Domain.Common;

namespace Transaction.Domain.Transactions.Events;

public sealed record TransactionUpdatedDomainEvent(
    Guid TransactionId
) : DomainEvent;