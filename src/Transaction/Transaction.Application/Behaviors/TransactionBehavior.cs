using MediatR;
using Microsoft.Extensions.Logging;
using Transaction.Application.Abstractions;

namespace Transaction.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior for database transaction management.
/// Wraps handler execution in a database transaction for atomicity.
/// Uses primary constructor pattern for modern C# 12+.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
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
            logger.LogDebug("Beginning database transaction for {RequestName}", requestName);

            // Execute handler within transaction scope
            var response = await next();

            // Commit transaction (SaveChangesAsync is already called in handler via UnitOfWork)
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogDebug("Transaction committed successfully for {RequestName}", requestName);

            return response;
        }
        catch (Exception ex)
        {
            // Transaction automatically rolled back on exception
            logger.LogError(
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