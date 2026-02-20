using Transaction.Application.IP;
using Transaction.Infrastructure.IP;

namespace Transaction.Api.Middleware;

/// <summary>
/// Middleware to extract client IP address and populate IpAddressContext.
/// Must be registered before handlers that need IP information.
/// </summary>
public sealed class IpAddressMiddleware
{
    private readonly RequestDelegate _next;

    public IpAddressMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IpAddressContext ipContext)
    {
        // Extract IP from HttpContext
        var clientIp = IpAddressExtractor.GetClientIpAddress(context);

        // Store in scoped service (accessible in handlers via DI)
        ipContext.ClientIpAddress = clientIp;

        // Store in HttpContext.Items (accessible in this request context)
        context.Items["ClientIpAddress"] = clientIp;

        // Add to response header for client visibility
        context.Response.Headers.Append("X-Client-IP", clientIp);

        await _next(context);
    }
}
