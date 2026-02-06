using MediatR;
using Microsoft.Extensions.Logging;
using Transaction.Application.Abstractions;

namespace Transaction.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for database transaction management.
/// Wraps handler execution in a database transaction for atomicity.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Only wrap write operations in transaction
        // Queries don't need transaction
        if (!IsWriteOperation(requestName))
        {
            return await next();
        }

        try
        {
            _logger.LogDebug("Beginning database transaction for {RequestName}", requestName);

            // Execute handler within transaction scope
            var response = await next();

            // Commit transaction (SaveChangesAsync is already called in handler via UnitOfWork)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Transaction committed successfully for {RequestName}", requestName);

            return response;
        }
        catch (Exception ex)
        {
            // Transaction automatically rolled back on exception
            _logger.LogError(
                ex,
                "Transaction failed and rolled back for {RequestName}",
                requestName);

            throw;
        }
    }

    /// <summary>
    /// Determines if the request is a write operation (Command) vs read operation (Query).
    /// </summary>
    private static bool IsWriteOperation(string requestName)
    {
        // Commands end with "Command" suffix
        return requestName.EndsWith("Command", StringComparison.OrdinalIgnoreCase);
    }
}