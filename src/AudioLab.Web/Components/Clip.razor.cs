using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Session;
using AudioLab.Web.Waveforms;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue clip, audio or MIDI: a title bar in the track colour over a body that holds the waveform or the notes.</summary>
/// <remarks>
/// A clip redraws only when something it shows changed, so a new revision costs the clips that changed and no others.
/// Every clip stays in the page (D17). The lifted copy that follows the pointer carries the overlay shadow and moves by
/// <c>left</c> and <c>top</c>; the clip it was lifted from is left as it was.
/// </remarks>
public sealed partial class Clip : IDisposable
{
    private AudioClip? _audio;

    private MidiClip? _midi;

    private (string, bool, bool, bool, bool, double, double?, double, double?, ClipWave) _shown;

    private bool _drawn;

    private ClipWave _wave = ClipWave.Loading;

    private bool _wasSelected;

    /// <summary>The clip, when it is an audio clip.</summary>
    [Parameter]
    public AudioClip? Audio { get; set; }

    /// <summary>The clip, when it is a MIDI clip.</summary>
    [Parameter]
    public MidiClip? Midi { get; set; }

    /// <summary>The colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>Where the clip's left edge is, in pixels from the start of the lane.</summary>
    [Parameter]
    public double Left { get; set; }

    /// <summary>The top edge in pixels from the top of the lanes, for the lifted copy; null for a clip that sits in its lane.</summary>
    [Parameter]
    public double? Top { get; set; }

    /// <summary>How wide the clip is, in pixels.</summary>
    [Parameter]
    public double Width { get; set; }

    /// <summary>How tall the clip is, in pixels, for the lifted copy.</summary>
    [Parameter]
    public double? Height { get; set; }

    /// <summary>Whether this is the lifted copy that follows the pointer during a drag.</summary>
    [Parameter]
    public bool Lifted { get; set; }

    /// <summary>Whether the clip is selected.</summary>
    [Parameter]
    public bool Selected { get; set; }

    /// <summary>Whether the clip, or its track, is muted; it is then drawn at 40% opacity.</summary>
    [Parameter]
    public bool Dimmed { get; set; }

    /// <summary>Whether this is the one clip of the arrangement that is a tab stop; the arrow keys move it.</summary>
    [Parameter]
    public bool TabStop { get; set; }

    /// <summary>How many pixels a second of the timeline takes, which picks the level of peaks an audio clip draws its waveform from.</summary>
    [Parameter]
    public double PixelsPerSecond { get; set; } = TimelineGeometry.Default.PixelsPerSecond;

    /// <summary>Raised with the clip's id when it is pressed or activated.</summary>
    [Parameter]
    public EventCallback<ClipId> OnSelect { get; set; }

    /// <summary>Raised with the key and the clip's id for a key the arrangement handles, such as an arrow.</summary>
    [Parameter]
    public EventCallback<(ClipId Clip, KeyboardEventArgs Key)> OnKey { get; set; }

    /// <summary>Raised with the clip's id and the press when the primary button goes down on it, which may begin a drag.</summary>
    [Parameter]
    public EventCallback<(ClipId Clip, PointerEventArgs Press)> OnPointerDown { get; set; }

    /// <summary>The id of the clip's element, so the arrangement can move the focus to it.</summary>
    public static string IdOf(ClipId clip) => $"clip-{clip.Value}";

    private ClipId Id => Audio?.Id ?? Midi!.Id;

    private string ElementId => IdOf(Id);

    private string Name => Audio?.Name ?? Midi!.Name;

    private NoteBox[] Notes => Midi is null ? [] : MidiNoteLayout.Boxes(Midi);

    [Inject]
    private WaveformService Waves { get; set; } = default!;

    // What an audio clip draws for its waveform: nothing while the peaks load, a path from them, or why there is none.
    private ClipWave Wave => _wave;

    private string PeaksName => Wave.Phase.ToString().ToLowerInvariant();

    private string Description => Midi is null
        ? WaveNote is null ? $"{Name}, audio clip" : $"{Name}, audio clip. {WaveNote}"
        : $"{Name}, MIDI clip";

    // Only a waveform that is not drawn from the file's peaks says anything about itself.
    private string? WaveNote => Wave.Phase switch
    {
        WavePhase.Loading => "The waveform is loading.",
        WavePhase.Failed => $"The waveform could not be loaded: {Wave.Reason} Select the clip to try again.",
        _ => null,
    };

    private string? Hint => Midi is null ? WaveNote : null;

    private string PlaceStyle
    {
        get
        {
            string place = string.Create(CultureInfo.InvariantCulture, $"--clip: {ColorToken}; left: {Left:0.##}px; width: {Width:0.##}px");
            return Top is double top && Height is double height
                ? place + string.Create(CultureInfo.InvariantCulture, $"; top: {top:0.##}px; bottom: auto; height: {height:0.##}px")
                : place;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Waves.Arrived -= OnPeaksArrived;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Waves.Arrived += OnPeaksArrived;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Audio is null)
        {
            return;
        }
        _wave = Waves.For(Audio, Width, PixelsPerSecond);
        // A clip that is selected while its waveform has failed asks for the peaks again, however it was selected.
        if (Selected && !_wasSelected && _wave.Phase == WavePhase.Failed)
        {
            Waves.RetryIfFailed(Audio);
        }
        _wasSelected = Selected;
    }

    // Only a clip of the file whose peaks came, or failed, draws again; the lanes around it are left as they are.
    private void OnPeaksArrived(string media)
    {
        if (Audio is { } audio && audio.Media.Value == media)
        {
            _wave = Waves.For(audio, Width, PixelsPerSecond);
            _ = InvokeAsync(StateHasChanged);
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        bool same = _drawn
            && Equals(_audio, Audio)
            && Equals(_midi, Midi)
            && _shown == (ColorToken, Selected, Dimmed, TabStop, Lifted, Left, Top, Width, Height, Wave);
        _audio = Audio;
        _midi = Midi;
        _shown = (ColorToken, Selected, Dimmed, TabStop, Lifted, Left, Top, Width, Height, Wave);
        _drawn = true;
        return !same;
    }

    private static string NoteStyle(NoteBox note)
    {
        return string.Create(CultureInfo.InvariantCulture, $"left: {note.X:0.##}%; top: {note.Y:0.##}%; width: {note.Width:0.##}%");
    }

    private Task SelectAsync()
    {
        RetryWave();
        return Lifted ? Task.CompletedTask : OnSelect.InvokeAsync(Id);
    }

    // Activating a clip whose waveform failed asks again, selected already or not: the label says to select the clip to try again.
    private void RetryWave()
    {
        if (!Lifted && Audio is not null && _wave.Phase == WavePhase.Failed)
        {
            Waves.RetryIfFailed(Audio);
        }
    }

    private Task KeyAsync(KeyboardEventArgs key)
    {
        if (key.Key is "Enter" or " ")
        {
            RetryWave();
            return OnSelect.InvokeAsync(Id);
        }
        return OnKey.InvokeAsync((Id, key));
    }

    private Task PressAsync(PointerEventArgs press)
    {
        return press.Button == 0 && !Lifted ? OnPointerDown.InvokeAsync((Id, press)) : Task.CompletedTask;
    }
}
