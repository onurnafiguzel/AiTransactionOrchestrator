using BuildingBlocks.Contracts.Observability;
using MassTransit;

namespace BuildingBlocks.Observability;

public sealed class CorrelationPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        var cid = CorrelationContext.CorrelationId;
        if (!string.IsNullOrWhiteSpace(cid))
            context.Headers.Set(Correlation.HeaderName, cid);

        return next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope(nameof(CorrelationPublishFilter<T>));
}
