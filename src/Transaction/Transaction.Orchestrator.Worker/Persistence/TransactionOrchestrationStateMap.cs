using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Transaction.Orchestrator.Worker.Persistence;

public sealed class TransactionOrchestrationStateMap : SagaClassMap<TransactionOrchestrationState>
{
    protected override void Configure(EntityTypeBuilder<TransactionOrchestrationState> entity, ModelBuilder model)
    {
        entity.ToTable("transaction_orchestrations");

        entity.HasKey(x => x.CorrelationId);

        entity.Property(x => x.CurrentState).HasMaxLength(64);

        entity.Property(x => x.TransactionId).IsRequired();
        entity.HasIndex(x => x.TransactionId).IsUnique();

        entity.Property(x => x.CorrelationKey).HasMaxLength(128);

        entity.Property(x => x.Currency).HasMaxLength(8);
        entity.Property(x => x.MerchantId).HasMaxLength(64);

        entity.Property(x => x.FraudExplanation).HasMaxLength(2048);

        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.UpdatedAtUtc).IsRequired();

        entity.Property(x => x.FraudTimeoutTokenId).HasColumnName("fraud_timeout_token_id");
        entity.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();
        entity.Property(x => x.TimedOutAtUtc).HasColumnName("timed_out_at_utc");

        entity.Property(x => x.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();

    }
}
