using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Transaction.Domain.Common;

namespace Transaction.Infrastructure.Persistence.Interceptors;

/// <summary>
/// SaveChangesAsync'ten sonra DomainEvent'leri dispatch eder
/// </summary>
public sealed class DomainEventDispatcherInterceptor(IMediator mediator)
    : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null)
            return await base.SavedChangesAsync(eventData, result, cancellationToken);

        // Tüm tracked entities'den DomainEvent'leri al
        var aggregateRoots = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var allEvents = aggregateRoots
            .SelectMany(ar => ar.DomainEvents)
            .ToList();

        // Context'i temizle (infinite loop'u önle)
        context.ChangeTracker.Clear();

        // Event'leri dispatch et
        foreach (var @event in allEvents)
        {
            await mediator.Publish(@event, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }
}
