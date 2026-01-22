using Microsoft.EntityFrameworkCore;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Infrastructure.Outbox;

public sealed class OutboxStore(TransactionDbContext db)
{
    public async Task<List<OutboxMessage>> ClaimBatchAsync(
        int batchSize,
        string lockedBy,
        TimeSpan lockFor,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var lockedUntil = now.Add(lockFor);

        // Postgres: atomik claim (update + returning)
        var sql = """
                WITH cte AS (
                    SELECT "Id"
                    FROM "outbox_messages"
                    WHERE "PublishedAtUtc" IS NULL
                      AND "FailedAtUtc" IS NULL
                      AND "NextAttemptAtUtc" <= now()
                      AND ("LockedUntilUtc" IS NULL OR "LockedUntilUtc" < now())
                    ORDER BY "OccurredAtUtc"
                    LIMIT {0}
                    FOR UPDATE SKIP LOCKED
                )
                UPDATE "outbox_messages" om
                SET "LockedBy" = {1},
                    "LockedUntilUtc" = {2}
                FROM cte
                WHERE om."Id" = cte."Id"
                RETURNING om.*;
                """;


        // NOTE: EF Core maps returning rows back to entity
        return await db.OutboxMessages
            .FromSqlRaw(sql, batchSize, lockedBy, lockedUntil)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
