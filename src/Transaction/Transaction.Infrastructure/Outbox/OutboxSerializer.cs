using System.Text.Json;

namespace Transaction.Infrastructure.Outbox;

public static class OutboxSerializer
{
    public static string Serialize<T>(T message)
        => JsonSerializer.Serialize(message);
}
