using System.Net;
using AudioLab.Core.Model;
using AudioLab.Server.Configuration;
using Microsoft.Extensions.Primitives;

namespace AudioLab.Server.Doors;

/// <summary>The desktop door: one local user, for a loopback peer that is not a web page from another origin.</summary>
/// <remarks>
/// A page the user has open can send requests to the loopback address, and the peer is then loopback. So the door also
/// requires a loopback Host (against DNS rebinding), refuses an Origin that is not the server's own, and refuses a
/// request a browser marks as anything but same-origin or user-initiated. A client that is not a browser sends neither
/// header and is let through.
/// </remarks>
public sealed class DesktopDoor : IDoor
{
    /// <summary>The door's name.</summary>
    public const string DoorName = "desktop";

    /// <inheritdoc />
    public string Name => DoorName;

    /// <inheritdoc />
    public DoorResult Identify(DoorRequest request)
    {
        if (request.RemoteAddress is not { } address || !IPAddress.IsLoopback(address))
        {
            return Refuse("the desktop door answers loopback peers only");
        }
        if (!request.Headers.TryGetValue("Host", out StringValues hosts) || hosts.Count != 1 || !IsLoopbackAuthority(hosts[0]))
        {
            return Refuse("the Host header is not a loopback address");
        }
        if (request.Headers.TryGetValue("Origin", out StringValues origins) && !IsOwnOrigin(origins, hosts[0], request.Scheme))
        {
            return Refuse("the request comes from another origin");
        }
        if (request.Headers.TryGetValue("Sec-Fetch-Site", out StringValues sites) && !IsSameOrigin(sites))
        {
            return Refuse("the browser marks the request as coming from another site");
        }
        return DoorResult.Allowed(UserId.Local);
    }

    private static DoorResult Refuse(string reason) => DoorResult.Denied(StatusCodes.Status403Forbidden, reason);

    private static bool IsLoopbackAuthority(string? authority)
    {
        return !string.IsNullOrEmpty(authority) && ListenGuard.IsLoopbackUrl($"http://{authority}");
    }

    // An origin is scheme, host and port: it must be the one the request was sent to, so a loopback page on another port is another origin.
    private static bool IsOwnOrigin(StringValues origins, string? host, string scheme)
    {
        if (origins.Count != 1 || !Uri.TryCreate(origins[0], UriKind.Absolute, out Uri? origin) || origin.Scheme != scheme)
        {
            return false;
        }
        Uri own = new($"{scheme}://{host}");
        return IsLoopbackAuthority(origin.Authority) && origin.Port == own.Port && string.Equals(origin.Host, own.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameOrigin(StringValues sites)
    {
        return sites.Count == 1 && sites[0] is "same-origin" or "none";
    }
}
