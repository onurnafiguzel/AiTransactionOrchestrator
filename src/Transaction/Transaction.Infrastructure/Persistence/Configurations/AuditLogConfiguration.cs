using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transaction.Domain.Audit;

namespace Transaction.Infrastructure.Persistence.Configurations;

/// <summary>
/// AuditLog entity'sinin veritabanı yapılandırmasını tanımlar
/// Tablo adı, sütunlar ve indexes'i kurur
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs","public");

        // Primary Key tanımı
        builder.HasKey(a => a.Id);

        // Özellik yapılandırmaları
        builder.Property(a => a.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(a => a.TableName)
            .HasColumnName("TableName")
            .HasColumnType("varchar(256)")
            .IsRequired();

        builder.Property(a => a.Action)
            .HasColumnName("Action")
            .HasColumnType("varchar(10)") // "Insert", "Update", "Delete"
            .IsRequired();

        builder.Property(a => a.OldValues)
            .HasColumnName("OldValues")
            .HasColumnType("jsonb") // PostgreSQL JSON tip - tüm eski değerler
            .IsRequired(false);

        builder.Property(a => a.NewValues)
            .HasColumnName("NewValues")
            .HasColumnType("jsonb") // PostgreSQL JSON tip - tüm yeni değerler
            .IsRequired(false);

        builder.Property(a => a.ChangedAt)
            .HasColumnName("ChangedAt")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(a => a.TransactionId)
            .HasColumnName("TransactionId")
            .IsRequired();

        builder.Property(a => a.EventOrApiName)
            .HasColumnName("EventOrApiName")
            .HasColumnType("varchar(512)")
            .IsRequired();

        builder.Property(a => a.UserId)
            .HasColumnName("UserId")
            .HasColumnType("varchar(256)")
            .IsRequired(false);

        builder.Property(a => a.EntitySnapshot)
            .HasColumnName("EntitySnapshot")
            .HasColumnType("jsonb") // PostgreSQL JSON tip - entity'nin tam snapshotu
            .IsRequired(false);

        // Performans optimizasyonu için indexes
        // Transaction ID ile sorguların hızlı olması için
        builder.HasIndex(a => a.TransactionId)
            .HasDatabaseName("IX_AuditLogs_TransactionId");

        // Tarih bazlı sorgular için
        builder.HasIndex(a => a.ChangedAt)
            .HasDatabaseName("IX_AuditLogs_ChangedAt");

        // Çok sütun index: TableName + Action + ChangedAt
        // Belirli bir tablo için belirli bir aksiyon tipi ve tarih aralığında sorgular için
        builder.HasIndex(a => new { a.TableName, a.Action, a.ChangedAt })
            .HasDatabaseName("IX_AuditLogs_TableName_Action_ChangedAt");

        // Kullanıcı bazlı sorgular için
        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_AuditLogs_UserId");
    }
}
