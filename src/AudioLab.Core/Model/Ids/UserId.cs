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
    /// <param name="door">The door's name, in letters and digits.</param>
    /// <param name="subject">The user as that door knows them; the door hashes anything outside the id characters.</param>
    public static UserId ForDoor(string door, string subject)
    {
        IdRules.Require(door);
        IdRules.Require(subject);
        return From($"{door}-{subject}");
    }

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value ?? string.Empty;
}
