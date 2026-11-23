using AudioLab.Core.Model;
using AudioLab.Web.Media;
using AudioLab.Web.Session;
using AudioLab.Web.Waveforms;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The drag in progress: a mark where the clip would land and the lifted clip. Only this component redraws on a pointer move.</summary>
public sealed partial class DragLayer : IDisposable
{
    /// <summary>The document, which holds the clip being dragged.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>How many pixels a second of the timeline takes, so the lifted clip draws the waveform its lane draws.</summary>
    [Parameter]
    public double PixelsPerSecond { get; set; } = TimelineGeometry.Default.PixelsPerSecond;

    [Inject]
    private ClipDragState State { get; set; } = default!;

    [Inject]
    private MediaDragState MediaDrags { get; set; } = default!;

    [Inject]
    private TrackColorMap Colors { get; set; } = default!;

    [Inject]
    private WaveformService Waves { get; set; } = default!;

    private ClipDrag? Drag => State.Current;

    /// <inheritdoc />
    public void Dispose()
    {
        State.Changed -= OnChanged;
        MediaDrags.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        State.Changed += OnChanged;
        MediaDrags.Changed += OnChanged;
    }

    private void OnChanged() => StateHasChanged();

    private ClipWave Wave(AudioClip? audio, double width) => audio is null ? ClipWave.Loading : Waves.For(audio, width, PixelsPerSecond);

    private Lifted? Found(ClipId clip)
    {
        foreach (Track track in Document.Tracks)
        {
            AudioClip? audio = track.Clips.FirstOrDefault(c => c.Id == clip);
            MidiClip? midi = track.MidiClips.FirstOrDefault(c => c.Id == clip);
            if (audio is not null || midi is not null)
            {
                return new Lifted(audio, midi, audio?.Color ?? midi?.Color ?? track.Color);
            }
        }
        return null;
    }

    private sealed record Lifted(AudioClip? Audio, MidiClip? Midi, string Color);
}
