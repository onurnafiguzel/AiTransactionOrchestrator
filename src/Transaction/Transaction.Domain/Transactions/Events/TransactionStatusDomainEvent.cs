using Transaction.Domain.Common;

namespace Transaction.Domain.Transactions.Events;

public sealed record TransactionStatusDomainEvent(
    Guid TransactionId
) : DomainEvent;
