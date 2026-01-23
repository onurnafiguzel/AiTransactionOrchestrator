using System.Text.Json;

namespace Support.Bot.Logic;

public static class TimelineDisplayMessageBuilder
{
    public static string Build(string eventType, string? detailsJson)
    {
        return eventType switch
        {
            "TransactionCreated" => "Transaction created.",
            "FraudCheckRequested" => BuildFraudRequested(detailsJson),
            "FraudCheckCompleted" => BuildFraudCompleted(detailsJson),
            "FraudTimeoutExpired" => BuildRetry(detailsJson),
            "TransactionTimedOut" => "Transaction timed out (fraud did not respond within the allowed window).",
            "TransactionApproved" => BuildApproved(detailsJson),
            "TransactionRejected" => BuildRejected(detailsJson),
            _ => eventType
        };
    }

    private static string BuildRetry(string? json)
    {
        var retry = TryGetInt(json, "retry");
        return retry is null ? "Fraud timeout occurred (retry scheduled)." : $"Fraud timeout occurred. Retry #{retry}.";
    }

    private static string BuildFraudRequested(string? json)
    {
        var amount = TryGetDecimal(json, "amount");
        var currency = TryGetString(json, "currency");
        return (amount, currency) switch
        {
            (not null, not null) => $"Fraud check requested for {amount:0.##} {currency}.",
            _ => "Fraud check requested."
        };
    }

    private static string BuildFraudCompleted(string? json)
    {
        var risk = TryGetInt(json, "riskScore");
        var decision = TryGetString(json, "decision");
        return (decision, risk) switch
        {
            (not null, not null) => $"Fraud check completed. Decision: {decision}. Risk score: {risk}.",
            (not null, null) => $"Fraud check completed. Decision: {decision}.",
            _ => "Fraud check completed."
        };
    }

    private static string BuildApproved(string? json)
    {
        var risk = TryGetInt(json, "riskScore");
        return risk is null ? "Transaction approved." : $"Transaction approved. Risk score: {risk}.";
    }

    private static string BuildRejected(string? json)
    {
        var reason = TryGetString(json, "reason");
        var risk = TryGetInt(json, "riskScore");

        if (reason is not null && risk is not null)
            return $"Transaction rejected. Reason: {reason}. Risk score: {risk}.";

        if (reason is not null)
            return $"Transaction rejected. Reason: {reason}.";

        return "Transaction rejected.";
    }

    private static int? TryGetInt(string? json, string prop)
        => TryGet(json, prop, e => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var v) ? v : (int?)null);

    private static decimal? TryGetDecimal(string? json, string prop)
        => TryGet(json, prop, e => e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var v) ? v : (decimal?)null);

    private static string? TryGetString(string? json, string prop)
        => TryGet(json, prop, e => e.ValueKind == JsonValueKind.String ? e.GetString() : null);

    private static T? TryGet<T>(string? json, string prop, Func<JsonElement, T?> selector) where T : struct
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(prop, out var el))
                return null;

            return selector(el);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGet(string? json, string prop, Func<JsonElement, string?> selector)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(prop, out var el))
                return null;

            return selector(el);
        }
        catch
        {
            return null;
        }
    }
}
