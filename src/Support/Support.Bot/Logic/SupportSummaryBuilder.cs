namespace Support.Bot.Logic;

public static class SupportSummaryBuilder
{
    public static (string summary, string? reason) Build(
        string status,
        string? decisionReason,
        int retryCount,
        DateTime? timedOutAtUtc)
    {
        if (status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            if (decisionReason?.Equals("TimedOut", StringComparison.OrdinalIgnoreCase) == true || timedOutAtUtc is not null)
                return ($"Transaction was rejected after timeout. Retries attempted: {retryCount}.", "TimedOut");

            return ("Transaction was rejected by fraud decision.", decisionReason ?? "FraudDecisionReject");
        }

        if (status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            return ("Transaction was approved by fraud decision.", null);

        return ($"Transaction is in status '{status}'.", null);
    }
}
