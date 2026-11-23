using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The top bar of the session: project, transport (not working yet), counter, toggles, tool, grid, who is here, export.</summary>
public sealed partial class TopBar
{
    private const string NoPlayback = "Playback arrives with the audio engine.";

    private const string NotYet = "This control arrives with the transport and the audio engine.";

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    private static IReadOnlyList<SegmentItem> TransportItems { get; } =
    [
        new("start", "Go to start", IconName.ToStart, Reason: NoPlayback),
        new("stop", "Stop", IconName.Stop, Reason: NoPlayback),
        new("play", "Play", IconName.Play, Reason: NoPlayback),
        new("record", "Record", IconName.Record, Reason: NoPlayback),
    ];

    private static IReadOnlyList<SegmentItem> ToolItems { get; } =
    [
        new("pointer", "Pointer tool", IconName.Pointer),
        new("range", "Range tool", IconName.Range, Reason: "The range tool arrives with range editing."),
    ];

    private static IReadOnlyList<PresenceMember> People { get; } = [new("You", "You")];

    private IReadOnlyList<CounterCell> Cells =>
    [
        new("TIME", TimeText.Counter(0), true),
        new("BARS", BarGrid.Position(Document, 0), true),
        new("TEMPO", Document.Bpm.ToString("0.00", CultureInfo.InvariantCulture)),
        new("METER", $"{Document.TimeSignature.Numerator}/{Document.TimeSignature.Denominator}"),
    ];

    private string Summary
    {
        get
        {
            int clips = Document.Tracks.Sum(track => track.Clips.Count + track.MidiClips.Count);
            int tracks = Document.Tracks.Count;
            return string.Create(CultureInfo.InvariantCulture, $"{tracks} {Plural(tracks, "track")}, {clips} {Plural(clips, "clip")}");
        }
    }

    private string GridText => Document.View.Grid.Length == 0 ? string.Empty : char.ToUpperInvariant(Document.View.Grid[0]) + Document.View.Grid[1..];

    private static string Plural(int count, string noun) => count == 1 ? noun : noun + "s";
}
