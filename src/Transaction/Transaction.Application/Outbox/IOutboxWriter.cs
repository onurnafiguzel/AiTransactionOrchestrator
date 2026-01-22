namespace Transaction.Application.Outbox;

public interface IOutboxWriter
{
    Task Enqueue<T>(T message, string correlationId, CancellationToken ct);
}
