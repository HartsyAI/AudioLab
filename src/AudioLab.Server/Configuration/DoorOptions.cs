using System.Text;

namespace AudioLab.Server.Configuration;

/// <summary>How requests are attributed to a user. The secret is never printed.</summary>
public sealed record DoorOptions
{
    /// <summary>The shortest proxy secret the server accepts, in characters.</summary>
    public const int MinSecretLength = 32;

    /// <summary>Which door this instance serves.</summary>
    public DoorKind Kind { get; init; } = DoorKind.Desktop;

    /// <summary>The secret a proxy door's proxy sends with every request; required for Swarm and Hosted.</summary>
    public string? ProxySecret { get; init; }

    /// <summary>The header that carries the proxy secret.</summary>
    public string SecretHeader { get; init; } = "X-AudioLab-Secret";

    /// <summary>The header that carries the user the proxy signed in.</summary>
    public string UserHeader { get; init; } = "X-AudioLab-User";

    // A record prints every member, and the secret must not reach a log.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Kind = ").Append(Kind);
        builder.Append(", SecretHeader = ").Append(SecretHeader);
        builder.Append(", UserHeader = ").Append(UserHeader);
        return true;
    }
}
