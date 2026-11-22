using System.Net;
using AudioLab.Core.Model;

namespace AudioLab.Server.Doors;

/// <summary>The desktop door: one local user, and only for a peer on the loopback interface.</summary>
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
            return DoorResult.Denied(StatusCodes.Status403Forbidden, "the desktop door answers loopback peers only");
        }
        return DoorResult.Allowed(UserId.Local);
    }
}
