using BuildingBlocks.Contracts.Transactions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Transaction.Application.Abstractions;
using Transaction.Infrastructure.Inbox;
using Transaction.Infrastructure.Persistence;

namespace Transaction.Updater.Worker.Consumers;

public sealed class TransactionApprovedConsumer(
    ITransactionRepository repo,
    TransactionDbContext db,
    IUnitOfWork uow,
    ILogger<TransactionApprovedConsumer> logger)
    : IConsumer<TransactionApproved>
{
    public async Task Consume(ConsumeContext<TransactionApproved> context)
    {
        var messageId = context.MessageId ?? Guid.NewGuid();

        if (await db.InboxMessages.AnyAsync(x => x.MessageId == messageId))
            return; // duplicate

        db.InboxMessages.Add(new InboxMessage(messageId));

        var tx = await repo.Get(context.Message.TransactionId, context.CancellationToken);
        if (tx is null) return;

        tx.MarkApproved(context.Message.RiskScore, context.Message.Explanation);
        await uow.SaveChangesAsync(context.CancellationToken);
    }
}
