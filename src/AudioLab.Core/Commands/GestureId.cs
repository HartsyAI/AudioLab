using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Names one continuous gesture, such as a drag, so its commands become one undo step.</summary>
/// <remarks>
/// The client picks it, because it starts the gesture before the server could answer; it never enters a document and
/// is only compared with the id of the gesture that is open, so it is not an allocated id.
/// </remarks>
[JsonConverter(typeof(IdJsonConverter<GestureId>))]
public readonly record struct GestureId : IId<GestureId>
{
    private readonly string? _value;

    private GestureId(string value) => _value = value;

    /// <inheritdoc />
    public string Value => _value ?? string.Empty;

    /// <inheritdoc />
    public static GestureId From(string value) => new(IdRules.Require(value));

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value;
}
