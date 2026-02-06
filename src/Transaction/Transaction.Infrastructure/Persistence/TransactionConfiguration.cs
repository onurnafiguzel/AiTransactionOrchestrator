using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transaction.Domain.Transactions;

namespace Transaction.Infrastructure.Persistence;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction.Domain.Transactions.Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction.Domain.Transactions.Transaction> b)
    {
        b.ToTable("transactions");

        b.HasKey(x => x.Id);

        b.Property(x => x.Amount)
            .HasColumnName("amount")
            .IsRequired();

        b.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasMaxLength(8)
            .IsRequired();

        b.Property(x => x.MerchantId)
            .HasColumnName("merchant_id")
            .HasMaxLength(64)
            .IsRequired();

        b.Property(x => x.CustomerIp)
            .HasColumnName("customer_ip")
            .HasMaxLength(45)  // IPv6 max length
            .IsRequired();

        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        b.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        b.Property(x => x.RiskScore)
            .HasColumnName("risk_score");

        b.Property(x => x.DecisionReason)
            .HasColumnName("decision_reason")
            .HasMaxLength(256);

        b.Property(x => x.Explanation)
            .HasColumnName("explanation")
            .HasMaxLength(4096);

        b.Property(x => x.LastDecidedAtUtc)
            .HasColumnName("last_decided_at_utc");
       
       // Index for fraud detection queries
        b.HasIndex(x => x.CustomerIp)
            .HasDatabaseName("idx_transactions_customer_ip");

        // Index for velocity checks
        b.HasIndex(x => new { x.CustomerIp, x.CreatedAtUtc })
            .HasDatabaseName("idx_transactions_customer_ip_created_at");
            
        // DomainEvents EF tarafından persist edilmesin
        b.Ignore(x => x.DomainEvents);
    }
}
