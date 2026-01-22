using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transaction.Infrastructure.Outbox;

namespace Transaction.Infrastructure.Persistence;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).IsRequired();
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.CorrelationId).IsRequired();
        b.HasIndex(x => x.PublishedAtUtc);

        // Publish edilmeyenleri hızlı çekmek için
        b.HasIndex(x => x.PublishedAtUtc);
    }
}