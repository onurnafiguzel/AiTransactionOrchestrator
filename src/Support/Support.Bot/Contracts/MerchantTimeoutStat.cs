namespace Support.Bot.Contracts;

public sealed record MerchantTimeoutStat(
    string MerchantId,
    int TimedOutCount
);