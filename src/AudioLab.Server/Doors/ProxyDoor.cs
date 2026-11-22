using System.Security.Cryptography;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Server.Configuration;
using Microsoft.Extensions.Primitives;

namespace AudioLab.Server.Doors;

/// <summary>A door behind a trusted proxy (Swarm or hosted): the proxy proves itself with a secret and names the user.</summary>
/// <remarks>
/// The user header is read only on a request that carries the secret. The subject is hashed into the user id, so any
/// subject maps to a valid id and two different subjects never share one.
/// </remarks>
public sealed class ProxyDoor : IDoor
{
    /// <summary>The longest subject the proxy may name, in characters.</summary>
    public const int MaxSubjectLength = 128;

    /// <summary>How many hex digits of the subject's hash go into the id: 160 bits, so a user id is short and collisions are not a concern.</summary>
    public const int SubjectHexLength = 40;

    private readonly byte[] _secret;
    private readonly string _secretHeader;
    private readonly string _userHeader;

    /// <summary>Creates a door that trusts the proxy that knows the secret in <paramref name="options"/>.</summary>
    /// <param name="name">The door's name: lower-case letters and digits, such as swarm or hosted.</param>
    /// <param name="options">The door's configuration; the secret must be present.</param>
    public ProxyDoor(string name, DoorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrEmpty(options.ProxySecret) || options.ProxySecret.Length < DoorOptions.MinSecretLength)
        {
            throw new ArgumentException($"A proxy door needs a secret of at least {DoorOptions.MinSecretLength} characters.", nameof(options));
        }
        UserId.ForDoor(name, "probe");
        Name = name;
        _secret = Encoding.UTF8.GetBytes(options.ProxySecret);
        _secretHeader = options.SecretHeader;
        _userHeader = options.UserHeader;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>The id of the user a proxy named as <paramref name="subject"/> at the door called <paramref name="doorName"/>.</summary>
    public static UserId UserFor(string doorName, string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(subject)));
        return UserId.ForDoor(doorName, hash[..SubjectHexLength]);
    }

    /// <inheritdoc />
    public DoorResult Identify(DoorRequest request)
    {
        if (!request.Headers.TryGetValue(_secretHeader, out StringValues secret) || secret.Count != 1)
        {
            return DoorResult.Denied(StatusCodes.Status403Forbidden, "the proxy secret is missing or repeated");
        }
        if (!SecretMatches(secret[0]))
        {
            return DoorResult.Denied(StatusCodes.Status403Forbidden, "the proxy secret is wrong");
        }
        if (!request.Headers.TryGetValue(_userHeader, out StringValues subject) || subject.Count != 1)
        {
            return DoorResult.Denied(StatusCodes.Status401Unauthorized, "the proxy named no user, or more than one");
        }
        string name = subject[0] ?? string.Empty;
        if (!IsSubject(name))
        {
            return DoorResult.Denied(StatusCodes.Status401Unauthorized, "the user the proxy named is not a printable ASCII name");
        }
        return DoorResult.Allowed(UserFor(Name, name));
    }

    private bool SecretMatches(string? offered)
    {
        return offered is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(offered), _secret);
    }

    private static bool IsSubject(string subject)
    {
        return subject.Length is >= 1 and <= MaxSubjectLength && subject.All(c => c is >= '!' and <= '~');
    }
}
