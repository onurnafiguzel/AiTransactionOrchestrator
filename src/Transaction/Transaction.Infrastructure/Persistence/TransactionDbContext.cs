using Microsoft.EntityFrameworkCore;

namespace Transaction.Infrastructure.Persistence;

public sealed class TransactionDbContext(DbContextOptions<TransactionDbContext> options)
    : DbContext(options)
{
    public DbSet<Transaction.Domain.Transactions.Transaction> Transactions => Set<Transaction.Domain.Transactions.Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
