using BuildingBlocks.Contracts.Observability;
using MassTransit;

namespace BuildingBlocks.Observability;

public sealed class CorrelationConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var cid = context.Headers.Get<string>(Correlation.HeaderName);

        if (string.IsNullOrWhiteSpace(cid) && context.CorrelationId.HasValue)
            cid = context.CorrelationId.Value.ToString("N");

        if (string.IsNullOrWhiteSpace(cid))
        {
            await next.Send(context);
            return;
        }

        // AsyncLocal (enricher için)
        CorrelationContext.CorrelationId = cid;

        // Scope (BeginScope etkisi): consumer içindeki tüm loglara otomatik ekler
        using (Serilog.Context.LogContext.PushProperty("correlation_id", cid))
        using (Serilog.Context.LogContext.PushProperty("message_id", context.MessageId?.ToString()))
        using (Serilog.Context.LogContext.PushProperty("conversation_id", context.ConversationId?.ToString()))
        {
            await next.Send(context);
        }
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope(nameof(CorrelationConsumeFilter<T>));
}
