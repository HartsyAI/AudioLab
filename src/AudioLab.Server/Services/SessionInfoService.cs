using AudioLab.Core.Api;
using AudioLab.Core.Model;
using AudioLab.Server.Doors;

namespace AudioLab.Server.Services;

/// <summary>Describes the caller to the caller.</summary>
public sealed class SessionInfoService(IDoor door)
{
    /// <summary>The door and user a request resolved to.</summary>
    public SessionInfo Describe(UserId user) => new(door.Name, user.Value);
}
