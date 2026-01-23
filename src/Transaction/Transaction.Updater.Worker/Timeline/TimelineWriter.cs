using Npgsql;

namespace Transaction.Updater.Worker.Timeline;

public sealed class TimelineWriter(string connectionString)
{
    public async Task Append(
        Guid transactionId,
        string eventType,
        string? detailsJson,
        string? correlationId,
        string source,
        CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                            insert into transaction_timeline
                            (id, transaction_id, event_type, details_json, occurred_at_utc, correlation_id, source)
                            values
                            (@id, @tx, @type, @json, @at, @corr, @src);
                            """;

        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@tx", transactionId);
        cmd.Parameters.AddWithValue("@type", eventType);
        cmd.Parameters.AddWithValue("@json", (object?)detailsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@corr", (object?)correlationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@src", source);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
