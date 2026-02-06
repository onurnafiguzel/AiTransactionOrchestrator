using MediatR;
using Transaction.Application.Abstractions;
using Transaction.Application.IP;
using Transaction.Application.Transactions;

namespace Transaction.Application.Transactions;

/// <summary>
/// Handler for CreateTransactionCommand.
/// Creates a new transaction with IP address for fraud detection.
/// Uses primary constructor pattern for modern C# 12+.
/// </summary>
public sealed class CreateTransactionHandler(
    ITransactionRepository repo,
    Transaction.Application.Outbox.IOutboxWriter outbox,
    IUnitOfWork uow,
    IpAddressContext ipContext)
    : IRequestHandler<CreateTransactionCommand, Guid>
{
    public async Task<Guid> Handle(CreateTransactionCommand request, CancellationToken ct)
    {
        var customerIp = ipContext.ClientIpAddress;

        var tx = Transaction.Domain.Transactions.Transaction.Create(
            request.Amount,
            request.Currency,
            request.MerchantId,
            customerIp);

        await repo.Add(tx, ct);

        await outbox.Enqueue(
            new BuildingBlocks.Contracts.Transactions.TransactionCreated(
                tx.Id,
                request.Amount,
                request.Currency,
                request.MerchantId,
                request.CorrelationId,
                customerIp),
            request.CorrelationId,
            ct);

        await uow.SaveChangesAsync(ct);
        return tx.Id;
    }
}
