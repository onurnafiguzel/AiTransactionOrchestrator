namespace Transaction.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public string CorrelationId { get; private set; } = default!;
    public string? IdempotencyKey { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    // Reliability fields
    public DateTime NextAttemptAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    // Claim/Lock
    public string? LockedBy { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }

    // Poison
    public DateTime? FailedAtUtc { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string payload, string correlationId, string? idempotencyKey = null)
    {
        Id = id;
        Type = type;
        Payload = payload;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;

        OccurredAtUtc = DateTime.UtcNow;
        NextAttemptAtUtc = OccurredAtUtc;
        AttemptCount = 0;
    }

    public void MarkPublished()
    {
        PublishedAtUtc = DateTime.UtcNow;
        LockedBy = null;
        LockedUntilUtc = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTime nextAttemptAtUtc, int maxAttempts)
    {
        AttemptCount += 1;
        LastError = Truncate(error, 2000);
        NextAttemptAtUtc = nextAttemptAtUtc;

        LockedBy = null;
        LockedUntilUtc = null;

        if (AttemptCount >= maxAttempts)
            FailedAtUtc = DateTime.UtcNow;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
