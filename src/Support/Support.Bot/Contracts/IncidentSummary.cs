namespace Support.Bot.Contracts;

public sealed record IncidentSummary(
    int WindowMinutes,
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalTransactions,
    int ApprovedCount,
    int RejectedCount,
    int TimedOutCount,
    decimal TimeoutRate,
    IReadOnlyList<MerchantTimeoutStat> TopMerchantsByTimeout
);
