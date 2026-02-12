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

    public async Task<(List<Domain.Transactions.Transaction> Items, int TotalCount)> GetAllPagedAsync(
        int skip,
        int take,
        string? sortBy,
        string sortDirection,
        CancellationToken ct = default)
    {
        var query = db.Transactions.AsNoTracking();

        // Apply sorting
        query = sortBy?.ToLower() switch
        {
            "amount" => sortDirection == "asc"
                ? query.OrderBy(t => t.Amount)
                : query.OrderByDescending(t => t.Amount),
            "status" => sortDirection == "asc"
                ? query.OrderBy(t => t.Status)
                : query.OrderByDescending(t => t.Status),
            "createdat" or "created" => sortDirection == "asc"
                ? query.OrderBy(t => t.CreatedAtUtc)
                : query.OrderByDescending(t => t.CreatedAtUtc),
            _ => sortDirection == "asc"
                ? query.OrderBy(t => t.UpdatedAtUtc)
                : query.OrderByDescending(t => t.UpdatedAtUtc)
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
