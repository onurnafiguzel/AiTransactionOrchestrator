using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Transaction.Infrastructure.Inbox;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> b)
    {
        b.ToTable("inbox_messages");

        b.HasKey(x => x.MessageId);

        b.Property(x => x.ProcessedAtUtc).IsRequired();

        // PK zaten unique -> dedup garantisi
    }
}
