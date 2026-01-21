using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Transaction.Infrastructure.Persistence;

public sealed class TransactionDbContextFactory : IDesignTimeDbContextFactory<TransactionDbContext>
{
    public TransactionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass")
            .Options;

        return new TransactionDbContext(options);
    }
}
