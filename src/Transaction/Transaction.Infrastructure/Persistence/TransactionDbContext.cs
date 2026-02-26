using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Transaction.Domain.Audit;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Outbox;
using Transaction.Infrastructure.Persistence.Configurations;
using Transaction.Infrastructure.Persistence.Interceptors;

namespace Transaction.Infrastructure.Persistence;

/// <summary>
/// Transaction modülü için ana DbContext
/// Domain entities'lerini, outbox/inbox messages'ları ve audit logs'u yönetir
/// </summary>
public sealed class TransactionDbContext(
    DbContextOptions<TransactionDbContext> options,
    IMediator? mediator = null,
    IHttpContextAccessor? httpContextAccessor = null)
    : DbContext(options)
{
    private readonly IMediator? _mediator = mediator;
    private readonly IHttpContextAccessor? _httpContextAccessor = httpContextAccessor;

    // Domain entities
    public DbSet<Transaction.Domain.Transactions.Transaction> Transactions => Set<Transaction.Domain.Transactions.Transaction>();
    public DbSet<Transaction.Domain.Users.User> Users => Set<Transaction.Domain.Users.User>();

    // Messaging patterns
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    // Audit trail - Veritabanında gerçekleşen tüm değişiklikleri kaydeder
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Domain Event Dispatcher Interceptor - Aggregate root'lardan events dispatch eder
        if (_mediator != null)
        {
            optionsBuilder.AddInterceptors(new DomainEventDispatcherInterceptor(_mediator));
        }

        // Audit Interceptor - Tüm veritabanı değişikliklerini kaydeder
        // IHttpContextAccessor'ı kullanarak API endpoint ya da event bilgisini yakalar
        if (_httpContextAccessor != null)
        {
            optionsBuilder.AddInterceptors(new AuditInterceptor(_httpContextAccessor, "Unknown"));
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Domain configurations
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());

        // Messaging configurations
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

        // Audit log configuration
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
