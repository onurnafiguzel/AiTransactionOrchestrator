using MediatR;
using Microsoft.EntityFrameworkCore;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Outbox;
using Transaction.Infrastructure.Persistence.Configurations;
using Transaction.Infrastructure.Persistence.Interceptors;

namespace Transaction.Infrastructure.Persistence;

public sealed class TransactionDbContext(
    DbContextOptions<TransactionDbContext> options,
    IMediator? mediator = null)
    : DbContext(options)
{
    private readonly IMediator? _mediator = mediator;

    public DbSet<Transaction.Domain.Transactions.Transaction> Transactions => Set<Transaction.Domain.Transactions.Transaction>();
    public DbSet<Transaction.Domain.Users.User> Users => Set<Transaction.Domain.Users.User>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_mediator != null)
        {
            optionsBuilder.AddInterceptors(new DomainEventDispatcherInterceptor(_mediator));
        }
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
