using Dapper;
using Npgsql;

namespace Support.Bot.Data;

public sealed class SupportReadRepository(string connectionString)
{
    public async Task<TransactionRow?> GetTransaction(Guid id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        const string sql = """
                            select
                                "Id",
                                status,
                                risk_score,
                                decision_reason,
                                explanation,
                                last_decided_at_utc,
                                updated_at_utc
                            from transactions
                            where "Id" = @id
                            """;
        return await conn.QuerySingleOrDefaultAsync<TransactionRow>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
    }

    public async Task<SagaRow?> GetSaga(Guid transactionId, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        const string sql = """
                            select
                                "TransactionId",
                                "CurrentState",
                                retry_count,
                                timed_out_at_utc,
                                "CorrelationKey"
                            from transaction_orchestrations
                            where "TransactionId" = @transactionId
                            """;

        return await conn.QuerySingleOrDefaultAsync<SagaRow>(new CommandDefinition(sql, new { transactionId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TimelineRow>> GetTimeline(Guid transactionId, int limit, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);

        const string sql = """
                            select event_type, details_json, occurred_at_utc, source
                            from transaction_timeline
                            where transaction_id = @transactionId
                            order by occurred_at_utc desc
                            limit @limit
                            """;

        var rows = await conn.QueryAsync<TimelineRow>(
            new CommandDefinition(sql, new { transactionId, limit }, cancellationToken: ct));

        return rows.ToList();
    }
}

public sealed record TransactionRow(
    Guid Id,
    int Status,
    int? Risk_Score,
    string? Decision_Reason,
    string? Explanation,
    DateTime? Last_Decided_At_Utc,
    DateTime Updated_At_Utc
);

public sealed record SagaRow(
    Guid TransactionId,
    string CurrentState,
    int retry_Count,
    DateTime? timed_out_at_utc,
    string CorrelationKey
);

public sealed record TimelineRow(string Event_Type, string? Details_Json, DateTime Occurred_At_Utc, string? Source);
