namespace Transaction.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = default!;

    private OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string payload, string correlationId)
    {
        Id = id;
        Type = type;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredAtUtc = DateTime.UtcNow;
    }

    public void MarkPublished()
    {
        PublishedAtUtc = DateTime.UtcNow;
    }
}
