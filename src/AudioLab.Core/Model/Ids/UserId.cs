using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies the user who owns a project or a media file. The door supplies it.</summary>
/// <param name="Value">The identifier text.</param>
[JsonConverter(typeof(IdJsonConverter<UserId>))]
public readonly record struct UserId(string Value) : IId<UserId>
{
    /// <summary>The one user of the desktop door, which has no sign-in.</summary>
    public static UserId Local { get; } = new("local");

    /// <inheritdoc />
    public static UserId From(string value) => new(value);

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
