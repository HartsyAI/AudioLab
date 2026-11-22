using AudioLab.Core.Model;

namespace AudioLab.Server.Doors;

/// <summary>The outcome of identifying a request: a user, or a refusal with an HTTP status and a reason for the log.</summary>
public readonly record struct DoorResult
{
    /// <summary>The user the request belongs to; null when the door refused it.</summary>
    public UserId? User { get; private init; }

    /// <summary>The HTTP status to answer with when there is no user.</summary>
    public int StatusCode { get; private init; }

    /// <summary>Why the door refused, for the server log only; never sent to the caller.</summary>
    public string? Reason { get; private init; }

    /// <summary>The request belongs to <paramref name="user"/>.</summary>
    public static DoorResult Allowed(UserId user) => new() { User = user };

    /// <summary>The request belongs to nobody.</summary>
    public static DoorResult Denied(int statusCode, string reason) => new() { StatusCode = statusCode, Reason = reason };
}
