using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The drag in progress: a mark where the clip would land and the lifted clip. Only this component redraws on a pointer move.</summary>
public sealed partial class DragLayer : IDisposable
{
    /// <summary>The document, which holds the clip being dragged.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    [Inject]
    private ClipDragState State { get; set; } = default!;

    [Inject]
    private TrackColorMap Colors { get; set; } = default!;

    [Inject]
    private WavePathCache Waves { get; set; } = default!;

    private ClipDrag? Drag => State.Current;

    /// <inheritdoc />
    public void Dispose()
    {
        State.Changed -= OnChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        State.Changed += OnChanged;
    }

    private void OnChanged() => StateHasChanged();

    private string? Waveform(AudioClip? audio, double width) => audio is null ? null : Waves.For(audio.Id.Value, width);

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
