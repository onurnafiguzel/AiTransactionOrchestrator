using System.Text.Json;
using BuildingBlocks.Contracts.Transactions;
using Microsoft.EntityFrameworkCore;
using Transaction.Application.Outbox;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Infrastructure.Outbox;

public sealed class EfCoreOutboxWriter(TransactionDbContext db) : IOutboxWriter
{
    public Task Enqueue<T>(T message, string correlationId, string? idempotencyKey, CancellationToken ct)
    {
        var type = message!.GetType().AssemblyQualifiedName
                   ?? throw new InvalidOperationException("Message type name not resolved.");

        var payload = System.Text.Json.JsonSerializer.Serialize(message);

        db.OutboxMessages.Add(new OutboxMessage(
            id: Guid.NewGuid(),
            type: type,
            payload: payload,
            correlationId: correlationId,
            idempotencyKey: idempotencyKey));

        return Task.CompletedTask;
    }

    public async Task<Guid?> TryGetExistingTransactionId(string idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var type = typeof(TransactionCreated).AssemblyQualifiedName
                   ?? throw new InvalidOperationException("Message type name not resolved.");

        var payload = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.IdempotencyKey == idempotencyKey && m.Type == type)
            .OrderByDescending(m => m.OccurredAtUtc)
            .Select(m => m.Payload)
            .FirstOrDefaultAsync(ct);

        if (payload is null)
        {
            return null;
        }

        try
        {
            var message = JsonSerializer.Deserialize<TransactionCreated>(payload);
            return message is null || message.TransactionId == Guid.Empty
                ? null
                : message.TransactionId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
