using BuildingBlocks.Contracts.Observability;
using Serilog.Context;

namespace Transaction.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(Correlation.HeaderName, out var values)
            ? values.ToString()
            : Guid.NewGuid().ToString("N");

        CorrelationContext.CorrelationId = correlationId;

        // Store in HttpContext.Items for downstream middleware (RequestResponseLoggingMiddleware)
        context.Items[Correlation.HeaderName] = correlationId;

        // Store in Serilog LogContext for structured logging
        LogContext.PushProperty("CorrelationId", correlationId);

        context.Response.Headers[Correlation.HeaderName] = correlationId;

        await next(context);
    }
}
