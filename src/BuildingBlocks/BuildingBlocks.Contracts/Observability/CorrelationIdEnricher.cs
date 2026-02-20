using BuildingBlocks.Contracts.Observability;
using Serilog.Core;
using Serilog.Events;

namespace BuildingBlocks.Observability;

public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = CorrelationContext.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var prop = propertyFactory.CreateProperty(
                "correlation_id",
                correlationId);

            logEvent.AddOrUpdateProperty(prop);
        }
    }
}
