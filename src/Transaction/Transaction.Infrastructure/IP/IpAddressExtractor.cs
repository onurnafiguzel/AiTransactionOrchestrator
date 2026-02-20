using Microsoft.AspNetCore.Http;

namespace Transaction.Infrastructure.IP;

/// <summary>
/// Extracts client IP address from HttpContext.
/// Handles proxy scenarios (X-Forwarded-For, X-Real-IP headers).
/// </summary>
public static class IpAddressExtractor
{
    /// <summary>
    /// Gets the client IP address from HttpContext.
    /// Checks multiple sources: direct, X-Forwarded-For, X-Real-IP, Cloudflare, etc.
    /// </summary>
    public static string GetClientIpAddress(HttpContext context)
    {
        if (context == null)
            return "0.0.0.0";

        try
        {
            // 1. Check X-Forwarded-For header (proxy/load balancer)
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var ips = forwardedFor.ToString().Split(',');
                var ip = ips[0].Trim();

                if (!string.IsNullOrWhiteSpace(ip) && IsValidIpAddress(ip))
                    return ip;
            }

            // 2. Check X-Real-IP header (nginx, Apache)
            if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
            {
                if (!string.IsNullOrWhiteSpace(realIp) && IsValidIpAddress(realIp.ToString()))
                    return realIp.ToString();
            }

            // 3. Check CF-Connecting-IP header (Cloudflare)
            if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cloudflareIp))
            {
                if (!string.IsNullOrWhiteSpace(cloudflareIp) && IsValidIpAddress(cloudflareIp.ToString()))
                    return cloudflareIp.ToString();
            }

            // 4. Direct connection (no proxy)
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(remoteIp) && IsValidIpAddress(remoteIp))
                return remoteIp;

            // 5. Fallback
            return "0.0.0.0";
        }
        catch
        {
            return "0.0.0.0";
        }
    }

    /// <summary>
    /// Validates if string is valid IP address format.
    /// Excludes localhost, loopback, and invalid IPs.
    /// </summary>
    private static bool IsValidIpAddress(string ip)
    {
        return !string.IsNullOrWhiteSpace(ip) &&
               System.Net.IPAddress.TryParse(ip, out _) &&
               !ip.StartsWith("127.") &&
               !ip.StartsWith("192.168.") &&
               ip != "0.0.0.0" &&
               ip != "::1";
    }
}
