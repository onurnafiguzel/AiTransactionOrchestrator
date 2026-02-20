using BuildingBlocks.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Support.Bot.Caching;
using Support.Bot.Contracts;
using Support.Bot.Data;
using Support.Bot.Logic;
using Transaction.Domain.Transactions;

namespace Support.Bot.Controllers;

/// <summary>
/// Customer support endpoints for transaction inquiry - ADMIN ONLY
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "Admin")]
public sealed class SupportController(
    SupportReadRepository repository,
    ISupportTransactionCacheService cacheService,
    ILogger<SupportController> logger)
    : ControllerBase
{
    /// <summary>
    /// Get detailed transaction report for customer support
    /// </summary>
    /// <param name="transactionId">Transaction ID to query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detailed transaction report with saga info and timeline (cached for 10 minutes)</returns>
    /// <response code="200">Transaction report found</response>
    /// <response code="404">Transaction not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("transactions/{transactionId:guid}")]
    public async Task<ActionResult<SupportTransactionReport>> GetTransaction(
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty)
            throw new ArgumentException("Transaction ID cannot be empty", nameof(transactionId));

        // Try cache first (10 minute TTL)
        var cachedReport = await cacheService.GetSupportTransactionAsync<SupportTransactionReport>(
            transactionId,
            cancellationToken);

        if (cachedReport is not null)
        {
            logger.LogDebug(
                "Support transaction report retrieved from cache | TransactionId={TransactionId}",
                transactionId);
            return Ok(cachedReport);
        }

        // Cache miss - query database
        var transaction = await repository.GetTransaction(transactionId, cancellationToken);
        if (transaction is null)
        {
            logger.LogWarning("Transaction not found for support | TransactionId={TransactionId}", transactionId);
            return NotFound(new { transactionId });
        }

        // Get saga state
        var saga = await repository.GetSaga(transactionId, cancellationToken);

        // Build summary
        var statusName = Enum.GetName(typeof(TransactionStatus), transaction.Status)
            ?? transaction.Status.ToString();

        var (summary, reason) = SupportSummaryBuilder.Build(
            status: statusName,
            decisionReason: transaction.Decision_Reason,
            retryCount: saga?.retry_Count ?? 0,
            timedOutAtUtc: saga?.timed_out_at_utc);

        // Get timeline
        var timelineRows = await repository.GetTimeline(transactionId, limit: 50, cancellationToken);
        var timeline = timelineRows
            .Select(x => new SupportTimelineItem(
                EventType: x.Event_Type,
                DisplayMessage: TimelineDisplayMessageBuilder.Build(x.Event_Type, x.Details_Json),
                DetailsJson: x.Details_Json,
                OccurredAtUtc: x.Occurred_At_Utc,
                Source: x.Source))
            .ToList();

        // Build report
        var report = new SupportTransactionReport(
            TransactionId: transactionId,
            Status: statusName,
            Reason: transaction.Decision_Reason ?? reason,
            Summary: summary,
            Explanation: transaction.Explanation,
            Saga: new SupportSagaInfo(
                CurrentState: saga?.CurrentState,
                RetryCount: saga?.retry_Count ?? 0,
                TimedOutAtUtc: saga?.timed_out_at_utc,
                CorrelationId: saga?.CorrelationKey),
            Timeline: timeline,
            GeneratedAtUtc: DateTime.UtcNow);

        // Cache report for 10 minutes
        await cacheService.SetSupportTransactionAsync(transactionId, report, 10, cancellationToken);
        logger.LogDebug(
            "Support transaction report cached | TransactionId={TransactionId}",
            transactionId);

        return Ok(report);
    }

    /// <summary>
    /// Get incident summary statistics for given time window
    /// </summary>
    /// <param name="minutes">Time window in minutes (default: 15, min: 1, max: 1440)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Incident statistics including counts and timeout rates (cached for 30 minutes per window)</returns>
    /// <response code="200">Incident summary retrieved</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("incidents/summary")]
    public async Task<ActionResult<IncidentSummary>> GetIncidentsSummary(
        [FromQuery] int? minutes,
        CancellationToken cancellationToken)
    {
        var windowMinutes = Math.Clamp(minutes ?? 15, 1, 24 * 60);
        var cacheKey = $"incidents:summary:{windowMinutes}";

        // Try cache first (30 minute TTL for incident summaries)
        var cachedSummary = await cacheService.GetIncidentSummaryAsync<IncidentSummary>(
            cacheKey,
            cancellationToken);

        if (cachedSummary is not null)
        {
            logger.LogDebug(
                "Incident summary retrieved from cache | WindowMinutes={WindowMinutes}",
                windowMinutes);
            return Ok(cachedSummary);
        }

        // Cache miss - query database
        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddMinutes(-windowMinutes);

        var counts = await repository.GetIncidentCounts(fromUtc, toUtc, cancellationToken);

        // Get top merchants by timeout
        IReadOnlyList<MerchantTimeoutStat> topMerchants = Array.Empty<MerchantTimeoutStat>();
        try
        {
            var rows = await repository.GetTopMerchantsByTimedOut(fromUtc, toUtc, limit: 5, cancellationToken);
            topMerchants = rows
                .Select(x => new MerchantTimeoutStat(x.MerchantId, x.TimedOutCount))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get top merchants by timeout");
            topMerchants = Array.Empty<MerchantTimeoutStat>();
        }

        // Calculate timeout rate
        var timeoutRate = counts.Total == 0
            ? 0m
            : Math.Round((decimal)counts.TimedOut / counts.Total, 4);

        // Build summary
        var summary = new IncidentSummary(
            WindowMinutes: windowMinutes,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            TotalTransactions: counts.Total,
            ApprovedCount: counts.Approved,
            RejectedCount: counts.Rejected,
            TimedOutCount: counts.TimedOut,
            TimeoutRate: timeoutRate,
            TopMerchantsByTimeout: topMerchants);

        // Cache summary for 30 minutes
        await cacheService.SetIncidentSummaryAsync(cacheKey, summary, 30, cancellationToken);
        logger.LogDebug(
            "Incident summary cached | WindowMinutes={WindowMinutes}",
            windowMinutes);

        return Ok(summary);
    }

    /// <summary>
    /// Get all transactions with pagination for support purposes
    /// </summary>
    /// <param name="request">Pagination request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of all transactions</returns>
    /// <response code="200">Transactions retrieved successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("transactions")]
    public async Task<ActionResult<PagedResponse<TransactionListRow>>> GetAllTransactions(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = request.Normalize();

        var (items, totalCount) = await repository.GetAllTransactionsPaged(
            normalized.Skip,
            normalized.PageSize,
            normalized.SortBy,
            normalized.SortDirection,
            cancellationToken);

        var response = new PagedResponse<TransactionListRow>(
            items,
            normalized.Page,
            normalized.PageSize,
            totalCount);

        logger.LogInformation(
            "Support transactions retrieved | Page={Page} PageSize={PageSize} TotalCount={TotalCount}",
            normalized.Page,
            normalized.PageSize,
            totalCount);

        return Ok(response);
    }
}
