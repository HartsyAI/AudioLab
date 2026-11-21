using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies the user who owns a project or a media file, as the door that signed them in names them.</summary>
[JsonConverter(typeof(IdJsonConverter<UserId>))]
public readonly record struct UserId : IId<UserId>
{
    private UserId(string value) => Value = value;

    /// <summary>The one user of the desktop door, which has no sign-in. No other door can produce this id.</summary>
    public static UserId Local { get; } = ForDoor("desktop", "local");

    /// <inheritdoc />
    public string Value { get; }

    /// <inheritdoc />
    public static UserId From(string value) => new(IdRules.Require(value));

    /// <summary>The id of a user of <paramref name="door"/>, so one door can never mint another's ids.</summary>
    /// <param name="door">The door's name: lower-case letters and digits, so the first hyphen ends it.</param>
    /// <param name="subject">The user as that door knows them, in lower-case letters and digits; hash anything else.</param>
    /// <remarks>
    /// <see cref="From"/> still parses any well-formed id, so an id read from a file proves nothing about the sender: the
    /// store takes the owner from the session that made the request, never from the owner a file names.
    /// </remarks>
    public static UserId ForDoor(string door, string subject)
    {
        RequireWord(door, nameof(door));
        RequireWord(subject, nameof(subject));
        return From($"{door}-{subject}");
    }

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value ?? string.Empty;

    private static void RequireWord(string value, string name)
    {
        if (string.IsNullOrEmpty(value) || !value.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')))
        {
            throw new ArgumentException("Use lower-case letters and digits only.", name);
        }
    }
}
