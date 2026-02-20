using BuildingBlocks.Contracts.Observability;
using BuildingBlocks.Contracts.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
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
[Authorize] 
public sealed class TransactionController(
    ISender mediator,
    ITransactionRepository repository,
    ITransactionCacheService cacheService,
    ILogger<TransactionController> logger)
    : ControllerBase
{
    private const string IdempotencyHeaderName = "X-Idempotency-Key";

    /// <summary>
    /// Create a new transaction
    /// </summary>
    /// <param name="request">Transaction creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created transaction with ID and correlation ID</returns>
    /// <response code="201">Transaction created successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Unauthorized - JWT token required</response>
    /// <response code="429">Too many requests - rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [EnableRateLimiting("transaction-create")]
    public async Task<ActionResult> Create(
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;        
        
        if (string.IsNullOrEmpty(userIdClaim))
        {
            return Unauthorized(new { error = "UserId not found in token" });
        }

        // Extract UserId from JWT claims
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            logger.LogWarning("Transaction creation failed - UserId not found in JWT claims");
            return Unauthorized(new { error = "UserId not found in token" });
        }

        if (!HttpContext.Request.Headers.TryGetValue(IdempotencyHeaderName, out var idempotencyValues)
            || string.IsNullOrWhiteSpace(idempotencyValues.ToString()))
        {
            return BadRequest(new { error = $"{IdempotencyHeaderName} header is required" });
        }

        var idempotencyKey = idempotencyValues.ToString();

        // Get or create correlation ID
        var correlationId = CorrelationContext.CorrelationId
            ?? (HttpContext.Request.Headers.TryGetValue(Correlation.HeaderName, out var values)
                ? values.ToString()
                : Guid.NewGuid().ToString("N"));

        // Create transaction via mediator
        var command = new CreateTransactionCommand(
            userId,
            request.Amount,
            request.Currency,
            request.MerchantId,
            correlationId,
            idempotencyKey);

        var transactionId = await mediator.Send(command, cancellationToken);

        logger.LogInformation(
            "Transaction created successfully | TransactionId={TransactionId} UserId={UserId} CorrelationId={CorrelationId}",
            transactionId,
            userId,
            correlationId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = transactionId },
            new
            {
                transactionId = transactionId,
                userId = userId,
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
    /// <response code="429">Too many requests - rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("transaction-query")]
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

    /// <summary>
    /// Get all transactions with pagination
    /// </summary>
    /// <param name="request">Pagination request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of transactions</returns>
    /// <response code="200">Transactions retrieved successfully</response>
    /// <response code="401">Unauthorized - JWT token required</response>
    /// <response code="429">Too many requests - rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [EnableRateLimiting("transaction-query")]
    public async Task<ActionResult<PagedResponse<object>>> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request.Normalize();

        var (items, totalCount) = await repository.GetAllPagedAsync(
            normalized.Skip,
            normalized.PageSize,
            normalized.SortBy,
            normalized.SortDirection,
            cancellationToken);

        var dtos = items.Select(t => new
        {
            t.Id,
            t.Amount,
            t.Currency,
            t.MerchantId,
            t.Status,
            t.CreatedAtUtc,
            t.UpdatedAtUtc,
            t.IsDeleted
        }).ToList();

        var response = new PagedResponse<object>(
            dtos,
            normalized.Page,
            normalized.PageSize,
            totalCount);

        logger.LogInformation(
            "Transactions retrieved | Page={Page} PageSize={PageSize} TotalCount={TotalCount}",
            normalized.Page,
            normalized.PageSize,
            totalCount);

        return Ok(response);
    }


    [HttpPost("debug")]
    [EnableRateLimiting("transaction-query")]
    public IActionResult DebugAuth()
    {
    var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
    
    return Ok(new
    {
        AuthorizationHeader = authHeader,
        IsAuthenticated = User.Identity?.IsAuthenticated,
        UserName = User.Identity?.Name,
        AuthenticationType = User.Identity?.AuthenticationType,
        ClaimsCount = User.Claims.Count(),
        Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
    });
    }
}

/// <summary>
/// Transaction creation request
/// </summary>
public sealed record CreateTransactionRequest(
    decimal Amount,
    string Currency,
    string MerchantId);
