using Microsoft.EntityFrameworkCore;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Infrastructure.Inbox;

public sealed class InboxGuard(TransactionDbContext db)
{
    public async Task<bool> TryBeginAsync(Guid messageId, CancellationToken ct)
    {
        db.InboxMessages.Add(new InboxMessage(messageId));

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // unique violation -> duplicate
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
