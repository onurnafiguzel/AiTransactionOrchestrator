using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Transaction.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory - Migrations oluşturmak için kullanılır
/// Runtime sırasında Dependency Injection container'dan oluşturulur
/// </summary>
public sealed class TransactionDbContextFactory : IDesignTimeDbContextFactory<TransactionDbContext>
{
    public TransactionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=ato_db;Username=ato;Password=ato_pass")
            .Options;

        // Design-time: null mediator ve null httpContextAccessor (migrations çalışması için)
        // Runtime sırasında bu parametreler Dependency Injection'tan sağlanacak
        return new TransactionDbContext(options, mediator: null, httpContextAccessor: null);
    }
}
