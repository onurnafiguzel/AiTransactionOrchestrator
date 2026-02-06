namespace Transaction.Application.IP;

/// <summary>
/// Scoped service to store and access client IP address across request pipeline.
/// Populated by IpAddressMiddleware from HttpContext.
/// </summary>
public sealed class IpAddressContext
{
    /// <summary>
    /// Client IP address extracted from request headers or direct connection.
    /// Default: "0.0.0.0" if extraction fails.
    /// </summary>
    public string ClientIpAddress { get; set; } = "0.0.0.0";
}
