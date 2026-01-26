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

    // Support properties for fraud decision
    public int? RiskScore { get; private set; }
    public string? DecisionReason { get; private set; }
    public string? Explanation { get; private set; }
    public DateTime? LastDecidedAtUtc { get; private set; }

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

        tx.RaiseDomainEvent(new TransactionCreatedDomainEvent(
            tx.Id));

        return tx;
    }

    public void MarkApproved(int riskScore, string explanation)
    {
        if (IsDeleted) throw new InvalidOperationException("Transaction deleted.");

        Status = TransactionStatus.Approved;
        UpdatedAtUtc = DateTime.UtcNow;

        // Support properties
        RiskScore = riskScore;
        DecisionReason = null;
        Explanation = explanation;
        LastDecidedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new TransactionStatusDomainEvent(Id));
    }

    public void MarkRejected(int riskScore, string reason, string explanation)
    {
        if (IsDeleted) throw new InvalidOperationException("Transaction deleted.");

        Status = TransactionStatus.Rejected;
        UpdatedAtUtc = DateTime.UtcNow;

        // Support properties
        RiskScore = riskScore;
        DecisionReason = reason;
        Explanation = explanation;
        LastDecidedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new TransactionStatusDomainEvent(Id));
    }

}
