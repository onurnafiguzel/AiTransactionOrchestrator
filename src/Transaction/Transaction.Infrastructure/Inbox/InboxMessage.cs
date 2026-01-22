namespace Transaction.Infrastructure.Inbox;

public sealed class InboxMessage
{
    public Guid MessageId { get; private set; }
    public DateTime ProcessedAtUtc { get; private set; }

    private InboxMessage() { }

    public InboxMessage(Guid messageId)
    {
        MessageId = messageId;
        ProcessedAtUtc = DateTime.UtcNow;
    }
}
