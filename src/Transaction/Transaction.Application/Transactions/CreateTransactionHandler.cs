using MediatR;
using Transaction.Application.Abstractions;
using Transaction.Application.Transactions;

public sealed class CreateTransactionHandler(
    ITransactionRepository repo,
    Transaction.Application.Outbox.IOutboxWriter outbox,
    IUnitOfWork uow)
    : IRequestHandler<CreateTransactionCommand, Guid>
{
    public async Task<Guid> Handle(CreateTransactionCommand request, CancellationToken ct)
    {
        var tx = Transaction.Domain.Transactions.Transaction.Create(
            request.Amount, request.Currency, request.MerchantId);

        await repo.Add(tx, ct);

        await outbox.Enqueue(
            new BuildingBlocks.Contracts.Transactions.TransactionCreated(
                tx.Id,
                request.Amount,
                request.Currency,
                request.MerchantId,
                request.CorrelationId),
            request.CorrelationId,
            ct);

        await uow.SaveChangesAsync(ct);
        return tx.Id;
    }
}
