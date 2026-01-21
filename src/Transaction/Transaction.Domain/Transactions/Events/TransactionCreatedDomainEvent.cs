using Transaction.Domain.Common;

namespace Transaction.Domain.Transactions.Events;

public sealed record TransactionCreatedDomainEvent(
    Guid TransactionId,
    decimal Amount,
    string Currency,
    string MerchantId
) : DomainEvent;
