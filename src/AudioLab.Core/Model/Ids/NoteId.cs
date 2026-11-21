using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>Identifies a note inside its MIDI clip: unique within the clip only, a plain number on the wire.</summary>
/// <param name="Value">The ordinal, starting at 1.</param>
[JsonConverter(typeof(NoteIdJsonConverter))]
public readonly record struct NoteId(int Value)
{
    /// <summary>The first id of a clip.</summary>
    public static NoteId First { get; } = new(1);

    /// <summary>The id after this one.</summary>
    public NoteId Next() => new(checked(Value + 1));

    /// <summary>The highest value an id may have, so <see cref="Next"/> can always follow it.</summary>
    public const int Max = int.MaxValue - 1;

    /// <summary>The identifier text.</summary>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
