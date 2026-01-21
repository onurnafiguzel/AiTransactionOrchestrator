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

        // DomainEvents EF tarafından persist edilmesin
        b.Ignore(x => x.DomainEvents);
    }
}
