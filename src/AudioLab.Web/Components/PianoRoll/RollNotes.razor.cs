using System.Globalization;
using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components.PianoRoll;

/// <summary>The notes of the clip as a few SVG paths, not an element each: one path for each step of velocity.</summary>
/// <remarks>
/// The drawing is in seconds across and pitch rows down and stretched over the clip, so a note is a rectangle of the path. It is built when the
/// notes or the scale change, and not for a selection or a gesture, which <see cref="RollSelection"/> and the overlay draw (D17, D55).
/// </remarks>
public sealed partial class RollNotes
{
    /// <summary>How many steps of velocity are drawn as brightness.</summary>
    public const int Levels = 8;

    private const double MinPixels = 2;

    private const double Inset = 0.06;

    private MidiClip? _built;

    private double _scale;

    private bool _rebuilt;

    private string[] _levels = [];

    /// <summary>The clip.</summary>
    [Parameter, EditorRequired]
    public MidiClip Clip { get; set; } = null!;

    /// <summary>The scale.</summary>
    [Parameter]
    public RollGeometry Geometry { get; set; }

    /// <summary>The colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>The step of brightness a velocity falls in, 0 for the softest to <see cref="Levels"/> less one.</summary>
    public static int Level(int velocity) => Math.Clamp((velocity - 1) * Levels / 127, 0, Levels - 1);

    /// <summary>The path data of rectangles for notes: one rectangle each, as wide as the note or at least <paramref name="leastSeconds"/>.</summary>
    public static string Rectangles(IEnumerable<MidiNote> notes, double clipLength, double leastSeconds)
    {
        ArgumentNullException.ThrowIfNull(notes);
        StringBuilder path = new();
        foreach (MidiNote note in notes)
        {
            Append(path, note, clipLength, leastSeconds);
        }
        return path.ToString();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _rebuilt = false;
        if (_built is not null && _scale == Geometry.PixelsPerSecond && (ReferenceEquals(_built, Clip) || _built.Equals(Clip)))
        {
            return;
        }
        _rebuilt = true;
        _built = Clip;
        _scale = Geometry.PixelsPerSecond;
        StringBuilder[] levels = [.. Enumerable.Range(0, Levels).Select(_ => new StringBuilder())];
        double least = MinPixels / Geometry.PixelsPerSecond;
        foreach (MidiNote note in Clip.Notes)
        {
            Append(levels[Level(note.Velocity)], note, Clip.Length, least);
        }
        _levels = [.. levels.Select(builder => builder.ToString())];
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _rebuilt;

    private static void Append(StringBuilder path, MidiNote note, double clipLength, double leastSeconds)
    {
        double width = Math.Max(leastSeconds, Math.Min(note.Duration, clipLength - note.Start));
        path.Append('M').Append(Format(note.Start)).Append(' ').Append(Format(RollGeometry.Pitches - 1 - note.Pitch + Inset))
            .Append('h').Append(Format(width)).Append('v').Append(Format(1 - (2 * Inset))).Append('h').Append('-').Append(Format(width)).Append('z');
    }

    private static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
