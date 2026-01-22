using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Transaction.Application.Abstractions;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Updater.Worker.Consumers;

public sealed class TransactionRejectedConsumer(
    ITransactionRepository repo,
    TransactionDbContext db,
    IUnitOfWork uow,
    ILogger<TransactionRejectedConsumer> logger)
    : IConsumer<TransactionRejected>
{
    public async Task Consume(ConsumeContext<TransactionRejected> context)
    {
        var messageId = context.MessageId ?? Guid.NewGuid();

        if (await db.InboxMessages.AnyAsync(x => x.MessageId == messageId))
            return; // duplicate

        db.InboxMessages.Add(new InboxMessage(messageId));

        var tx = await repo.Get(context.Message.TransactionId, context.CancellationToken);
        if (tx is null) return;

        tx.MarkRejected(context.Message.RiskScore, context.Message.Reason, context.Message.Explanation);
        await uow.SaveChangesAsync(context.CancellationToken);
    }
}