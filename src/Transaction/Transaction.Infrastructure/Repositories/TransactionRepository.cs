using Microsoft.EntityFrameworkCore;
using Transaction.Application.Abstractions;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Infrastructure.Repositories;

public sealed class TransactionRepository(TransactionDbContext db) : ITransactionRepository
{
    public async Task Add(Transaction.Domain.Transactions.Transaction transaction, CancellationToken ct = default)
    {
        await db.Transactions.AddAsync(transaction, ct);
    }

    public Task<Transaction.Domain.Transactions.Transaction?> Get(Guid id, CancellationToken ct = default)
    {
        return db.Transactions
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }    

    public async Task Save(Transaction.Domain.Transactions.Transaction transaction, CancellationToken ct = default)
    {
        db.Transactions.Update(transaction);
        await db.SaveChangesAsync(ct);
    }
}
