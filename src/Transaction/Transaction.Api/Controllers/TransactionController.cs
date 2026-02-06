using BuildingBlocks.Contracts.Observability;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Transaction.Application.Abstractions;
using Transaction.Application.Transactions;
using Transaction.Infrastructure.Caching;

namespace Transaction.Api.Controllers;

/// <summary>
/// Transaction management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class TransactionController(
    ISender mediator,
    ITransactionRepository repository,
    ITransactionCacheService cacheService,
    ILogger<TransactionController> logger)
    : ControllerBase
{
    /// <summary>
    /// Create a new transaction
    /// </summary>
    /// <param name="request">Transaction creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created transaction with ID and correlation ID</returns>
    /// <response code="201">Transaction created successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    public async Task<ActionResult> Create(
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        // Validate request
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(request.Amount));

        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Currency is required", nameof(request.Currency));

        if (string.IsNullOrWhiteSpace(request.MerchantId))
            throw new ArgumentException("Merchant ID is required", nameof(request.MerchantId));

        // Get or create correlation ID
        var correlationId = CorrelationContext.CorrelationId
            ?? (HttpContext.Request.Headers.TryGetValue(Correlation.HeaderName, out var values)
                ? values.ToString()
                : Guid.NewGuid().ToString("N"));

        // Create transaction via mediator
        var command = new CreateTransactionCommand(
            request.Amount,
            request.Currency,
            request.MerchantId,
            correlationId);

        var transactionId = await mediator.Send(command, cancellationToken);

        logger.LogInformation(
            "Transaction created successfully | TransactionId={TransactionId} CorrelationId={CorrelationId}",
            transactionId,
            correlationId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = transactionId },
            new
            {
                transactionId = transactionId,
                correlationId = correlationId
            });
    }

    /// <summary>
    /// Get transaction by ID
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transaction details (cached for 10 minutes)</returns>
    /// <response code="200">Transaction found</response>
    /// <response code="404">Transaction not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Transaction ID cannot be empty", nameof(id));

        // Try cache first (10 minute TTL)
        var cachedTransaction = await cacheService.GetTransactionAsync<object>(id, cancellationToken);
        if (cachedTransaction is not null)
        {
            logger.LogDebug("Transaction retrieved from cache | TransactionId={TransactionId}", id);
            return Ok(cachedTransaction);
        }

        // Cache miss - query database
        var transaction = await repository.Get(id, cancellationToken);
        if (transaction is null)
        {
            logger.LogWarning("Transaction not found | TransactionId={TransactionId}", id);
            return NotFound();
        }

        // Build response
        var response = new
        {
            transaction.Id,
            transaction.Amount,
            transaction.Currency,
            transaction.MerchantId,
            transaction.Status,
            transaction.CreatedAtUtc,
            transaction.UpdatedAtUtc,
            transaction.IsDeleted
        };

        // Cache response for 10 minutes
        await cacheService.SetTransactionAsync(id, response, ttlMinutes: 10, cancellationToken);
        logger.LogDebug("Transaction cached | TransactionId={TransactionId}", id);

        return Ok(response);
    }
}

/// <summary>
/// Transaction creation request
/// </summary>
public sealed record CreateTransactionRequest(
    decimal Amount,
    string Currency,
    string MerchantId);
