using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue clip, audio or MIDI: a title bar in the track colour over a body that holds the waveform or the notes.</summary>
/// <remarks>
/// A clip redraws only when something it shows changed, so a new revision costs the clips that changed and no others.
/// Every clip stays in the page (D17). The lifted copy that follows the pointer carries the overlay shadow and moves by
/// <c>left</c> and <c>top</c>; the clip it was lifted from is left as it was.
/// </remarks>
public sealed partial class Clip
{
    private AudioClip? _audio;

    private MidiClip? _midi;

    private (string, bool, bool, bool, bool, double, double?, double, double?, string?) _shown;

    private bool _drawn;

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

    /// <summary>The path of the waveform stand-in of an audio clip; see <see cref="PlaceholderWaveform"/>.</summary>
    [Parameter]
    public string? Waveform { get; set; }

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

    private string Description => string.Create(CultureInfo.InvariantCulture, $"{Name}, {(Midi is null ? "audio" : "MIDI")} clip");

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
    protected override bool ShouldRender()
    {
        bool same = _drawn
            && Equals(_audio, Audio)
            && Equals(_midi, Midi)
            && _shown == (ColorToken, Selected, Dimmed, TabStop, Lifted, Left, Top, Width, Height, Waveform);
        _audio = Audio;
        _midi = Midi;
        _shown = (ColorToken, Selected, Dimmed, TabStop, Lifted, Left, Top, Width, Height, Waveform);
        _drawn = true;
        return !same;
    }

    private static string NoteStyle(NoteBox note)
    {
        return string.Create(CultureInfo.InvariantCulture, $"left: {note.X:0.##}%; top: {note.Y:0.##}%; width: {note.Width:0.##}%");
    }

    private Task SelectAsync()
    {
        return Lifted ? Task.CompletedTask : OnSelect.InvokeAsync(Id);
    }

    private Task KeyAsync(KeyboardEventArgs key)
    {
        return key.Key is "Enter" or " " ? OnSelect.InvokeAsync(Id) : OnKey.InvokeAsync((Id, key));
    }

    private Task PressAsync(PointerEventArgs press)
    {
        return press.Button == 0 && !Lifted ? OnPointerDown.InvokeAsync((Id, press)) : Task.CompletedTask;
    }
}
