using MediatR;

namespace Transaction.Application.Transactions;

public sealed record CreateTransactionCommand(
    Guid UserId,
    decimal Amount,
    string Currency,
    string MerchantId,
    string CorrelationId,
    string IdempotencyKey
) : IRequest<Guid>;
