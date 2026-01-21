using MediatR;
using Transaction.Application.Abstractions;

namespace Transaction.Application.Transactions;

public sealed class CreateTransactionHandler(ITransactionRepository repo)
    : IRequestHandler<CreateTransactionCommand, Guid>
{
    public async Task<Guid> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var tx = Transaction.Domain.Transactions.Transaction.Create(
            request.Amount,
            request.Currency,
            request.MerchantId);

        await repo.Add(tx, cancellationToken);
        return tx.Id;
    }
}
