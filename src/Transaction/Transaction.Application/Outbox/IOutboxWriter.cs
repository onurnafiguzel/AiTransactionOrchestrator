namespace Transaction.Application.Outbox;

public interface IOutboxWriter
{
    Task Enqueue<T>(T message, string correlationId, string? idempotencyKey, CancellationToken ct);
    Task<Guid?> TryGetExistingTransactionId(string idempotencyKey, CancellationToken ct);
}
