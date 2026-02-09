using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Transaction.Domain.Users;

namespace Transaction.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(u => u.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(u => u.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(u => u.LastLoginAtUtc)
            .HasColumnName("last_login_at_utc");

        builder.Property(u => u.DeactivatedAtUtc)
            .HasColumnName("deactivated_at_utc");

        builder.Property(u => u.DeactivationReason)
            .HasColumnName("deactivation_reason")
            .HasMaxLength(500);

        // Global query filter for soft delete
        builder.HasQueryFilter(u => !u.IsDeleted);

        // Ignore domain events (handled by EF interceptor)
        builder.Ignore(u => u.DomainEvents);
    }
}
