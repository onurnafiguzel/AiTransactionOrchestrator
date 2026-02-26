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

        b.Property(x => x.Type).IsRequired().HasMaxLength(512);
        b.Property(x => x.Payload).IsRequired();
        b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(128);
        b.Property(x => x.IdempotencyKey).HasMaxLength(128);

        b.Property(x => x.OccurredAtUtc).IsRequired();
        b.Property(x => x.PublishedAtUtc);

        b.Property(x => x.NextAttemptAtUtc).IsRequired();
        b.Property(x => x.AttemptCount).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2000);

        b.Property(x => x.LockedBy).HasMaxLength(64);
        b.Property(x => x.LockedUntilUtc);

        b.Property(x => x.FailedAtUtc);

        // Hot path index: pending & due
        b.HasIndex(x => new { x.PublishedAtUtc, x.FailedAtUtc, x.NextAttemptAtUtc });

        // Lock cleanup
        b.HasIndex(x => x.LockedUntilUtc);

        // Idempotency lookup
        b.HasIndex(x => new { x.IdempotencyKey, x.Type });
    }
}