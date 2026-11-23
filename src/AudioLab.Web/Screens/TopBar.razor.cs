using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Playback;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The top bar of the session: project, transport, counter, toggles, tool, grid, who is here, export.</summary>
public sealed partial class TopBar : IDisposable
{
    private const string NoEngine = "This browser cannot run the audio engine: it needs an audio worklet and a web worker.";

    private (bool Playing, bool Available, bool Looping) _drawn;

    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    [Inject]
    private PlaybackService Playback { get; set; } = default!;

    private bool Playing => Playback.Transport == TransportState.Playing;

    private string? PlaybackReason => Playback.Available ? null : NoEngine;

    private IReadOnlyList<SegmentItem> TransportItems =>
    [
        new("start", "Go to start", IconName.ToStart, Reason: PlaybackReason, Shortcut: Shortcuts.ToStart.Display),
        new("stop", "Stop", IconName.Stop, Reason: PlaybackReason, Shortcut: Shortcuts.Stop.Display),
        // One name, Play, whatever it is doing: the item is a toggle, so a screen reader says Play, pressed, and not a name that changes under it.
        new("play", "Play", Playing ? IconName.Pause : IconName.Play, Reason: PlaybackReason, Shortcut: Shortcuts.PlayPause.Display),
        new("record", "Record", IconName.Record, Reason: "Recording arrives with the audio devices."),
    ];

    private static IReadOnlyList<SegmentItem> ToolItems { get; } =
    [
        new("pointer", "Pointer tool", IconName.Pointer),
        new("range", "Range tool", IconName.Range, Reason: "The range tool arrives with range editing."),
    ];

    private static IReadOnlyList<PresenceMember> People { get; } = [new("You", "You")];

    /// <inheritdoc />
    public void Dispose()
    {
        Playback.Changed -= OnPlaybackChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        _drawn = Snapshot();
        Playback.Changed += OnPlaybackChanged;
    }

    // The transport changes the bar only when it starts or stops: the position counts in a component of its own.
    private (bool, bool, bool) Snapshot() => (Playing, Playback.Available, Playback.Looping);

    private void OnPlaybackChanged()
    {
        if (Snapshot() != _drawn)
        {
            _ = InvokeAsync(() =>
            {
                _drawn = Snapshot();
                StateHasChanged();
            });
        }
    }

    private async Task ChooseAsync(string id)
    {
        switch (id)
        {
            case "start":
                Playback.Seek(0);
                break;
            case "stop":
                Playback.Stop();
                break;
            case "play":
                await Playback.TogglePlayAsync();
                break;
        }
    }

    private void ToggleLoop() => Playback.SetLoop(!Playback.Looping);

    private Task ToggleClickAsync() => Actions.SetMetronome(!Document.Metronome);

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
