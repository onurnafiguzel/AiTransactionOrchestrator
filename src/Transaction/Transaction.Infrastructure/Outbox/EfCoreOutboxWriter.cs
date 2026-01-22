using Transaction.Application.Outbox;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Infrastructure.Outbox;

public sealed class EfCoreOutboxWriter(TransactionDbContext db) : IOutboxWriter
{
    public Task Enqueue<T>(T message, string correlationId, CancellationToken ct)
    {
        var type = message!.GetType().AssemblyQualifiedName
                   ?? throw new InvalidOperationException("Message type name not resolved.");

        var payload = System.Text.Json.JsonSerializer.Serialize(message);

        db.OutboxMessages.Add(new OutboxMessage(
            id: Guid.NewGuid(),
            type: type,
            payload: payload,
            correlationId: correlationId));

        return Task.CompletedTask;
    }
}
