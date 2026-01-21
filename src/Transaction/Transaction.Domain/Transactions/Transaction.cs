using Transaction.Domain.Common;
using Transaction.Domain.Transactions.Events;

namespace Transaction.Domain.Transactions;

// Aggregate Root
public sealed class Transaction : AggregateRoot
{
    // State
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = default!;
    public string MerchantId { get; private set; } = default!;
    public TransactionStatus Status { get; private set; }
    public bool IsDeleted { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Transaction() { } // ORM için; şimdi kullanılmıyor ama DDD’de standart

    // CREATE
    public static Transaction Create(decimal amount, string currency, string merchantId)
    {
        Guard.AgainstNegativeOrZero(amount, nameof(amount));
        Guard.AgainstNullOrWhiteSpace(currency, nameof(currency));
        Guard.AgainstNullOrWhiteSpace(merchantId, nameof(merchantId));

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            MerchantId = merchantId.Trim(),
            Status = TransactionStatus.Initiated,
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        tx.AddDomainEvent(new TransactionCreatedDomainEvent(tx.Id, tx.Amount, tx.Currency, tx.MerchantId));
        return tx;
    }

    // UPDATE
    public void Update(decimal amount, string currency)
    {
        EnsureNotDeleted();
        EnsureMutable();

        Guard.AgainstNegativeOrZero(amount, nameof(amount));
        Guard.AgainstNullOrWhiteSpace(currency, nameof(currency));

        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new TransactionUpdatedDomainEvent(Id));
    }

    // DELETE (domain’de soft delete; infra’da physical delete olabilir)
    public void Delete()
    {
        EnsureNotDeleted();

        // İş kuralı: Approved/Rejected transaction silinemez (istersen değiştiririz)
        Guard.Against(Status is TransactionStatus.Approved or TransactionStatus.Rejected,
            "Approved/Rejected transactions cannot be deleted.");

        IsDeleted = true;
        Status = TransactionStatus.Deleted;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new TransactionDeletedDomainEvent(Id));
    }

    // Domain behavior (ileride saga/worker ile çağrılacak)
    public void MarkPendingFraudCheck()
    {
        EnsureNotDeleted();
        EnsureMutable();

        Status = TransactionStatus.PendingFraudCheck;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkApproved(int riskScore, string explanation)
    {
        if (IsDeleted) throw new InvalidOperationException("Transaction deleted.");

        Status = TransactionStatus.Approved;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkRejected(int riskScore, string reason, string explanation)
    {
        if (IsDeleted) throw new InvalidOperationException("Transaction deleted.");

        Status = TransactionStatus.Rejected;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void EnsureNotDeleted()
        => Guard.Against(IsDeleted, "Transaction is deleted.");

    private void EnsureMutable()
        => Guard.Against(Status is TransactionStatus.Approved or TransactionStatus.Rejected,
            "Approved/Rejected transactions cannot be changed.");
}
