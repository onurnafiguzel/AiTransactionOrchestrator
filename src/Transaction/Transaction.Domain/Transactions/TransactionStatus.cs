namespace Transaction.Domain.Transactions;

public enum TransactionStatus
{
    Initiated = 1,
    PendingFraudCheck = 2,
    Approved = 3,
    Rejected = 4,
    Deleted = 99
}
