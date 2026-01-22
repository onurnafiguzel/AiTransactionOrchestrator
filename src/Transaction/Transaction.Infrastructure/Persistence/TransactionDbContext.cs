using Microsoft.EntityFrameworkCore;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Outbox;

namespace Transaction.Infrastructure.Persistence;

public sealed class TransactionDbContext(DbContextOptions<TransactionDbContext> options)
    : DbContext(options)
{
    public DbSet<Transaction.Domain.Transactions.Transaction> Transactions => Set<Transaction.Domain.Transactions.Transaction>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
