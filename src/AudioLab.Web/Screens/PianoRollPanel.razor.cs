using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The piano roll tab: the MIDI clip that is selected in the arrangement, or the way to make one.</summary>
public sealed partial class PianoRollPanel : IDisposable
{
    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    private (Track Track, MidiClip Clip)? Open => Selection.Clip is { } clip ? MidiClipPlan.Find(Document, clip) : null;

    // The selected track when it is an instrument track and no clip of it is open: where a new clip goes.
    private Track? Target => Document.Tracks.FirstOrDefault(track => track.Id == Selection.Track && track.Kind == TrackKind.Instrument);

    private bool HasInstrument => Document.Tracks.Any(track => track.Kind == TrackKind.Instrument);

    private string EmptyMessage => Target is { } track
        ? $"Add a MIDI clip to {track.Name}, or select one in the arrangement."
        : HasInstrument
            ? "Select a MIDI clip in the arrangement, or double-click an instrument lane to make one."
            : "MIDI clips live on instrument tracks. Add an instrument track, then a MIDI clip on it.";

    /// <inheritdoc />
    public void Dispose()
    {
        Selection.Changed -= OnSelectionChanged;
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        Selection.Changed += OnSelectionChanged;
    }

    // The buttons of the empty state go when they are used, and the focus would go with them: it moves to the clip that was made, or to the
    // button that replaces them.
    private const string AddId = "piano-roll-add";

    private string? _focus;

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (_focus is { } id && (id != AddId || Target is not null))
        {
            _focus = null;
            Focus.Focus(id);
        }
    }

    private async Task AddInstrumentAsync()
    {
        _focus = AddId;
        await Actions.AddInstrumentTrack();
    }

    private void OnSelectionChanged() => _ = InvokeAsync(StateHasChanged);

    private async Task AddAsync(Track track)
    {
        EditResult result = await Actions.Midi.AddClip(track.Id, null);
        if (result.Succeeded && result.Created.Count > 0)
        {
            ClipId made = ClipId.From(result.Created[0]);
            Selection.SelectClip(track.Id, made);
            _focus = Clip.IdOf(made);
        }
    }
}
