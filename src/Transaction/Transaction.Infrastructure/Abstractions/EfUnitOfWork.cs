using Transaction.Application.Abstractions;

namespace Transaction.Infrastructure.Persistence;

public sealed class EfUnitOfWork(TransactionDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
