using MediatR;

namespace Transaction.Application.Transactions;

public sealed record CreateTransactionCommand(
    decimal Amount,
    string Currency,
    string MerchantId,
    string CorrelationId
) : IRequest<Guid>;
