namespace Transaction.Application.Abstractions;

public interface ITransactionRepository
{
    Task Add(Domain.Transactions.Transaction transaction, CancellationToken ct = default);
    Task<Domain.Transactions.Transaction?> Get(Guid id, CancellationToken ct = default);
    Task Save(Domain.Transactions.Transaction transaction, CancellationToken ct = default);
    Task<(List<Domain.Transactions.Transaction> Items, int TotalCount)> GetAllPagedAsync(
        int skip,
        int take,
        string? sortBy,
        string sortDirection,
        CancellationToken ct = default);
}
