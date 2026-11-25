using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Media;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using AudioLab.Web.Session.Roll;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Pages;

/// <summary>The route of one open project: it joins over the hub, draws every revision it is sent, and makes the edits the screen offers.</summary>
public sealed partial class SessionPage
{
    private LiveProject? _live;

    private SessionEditor? _editor;

    private MidiEditor? _midi;

    private EffectEditor? _effects;

    private BeatEditor? _beats;
    private AutomationEditor? _automation;

    private SessionActions _actions = SessionActions.ReadOnly("Opening the project.");

    private string? _opened;

    private string? _failure;

    private TrackId? _renaming;

    private string _name = string.Empty;

    private string? _renameError;

    private bool _busy;

    private bool _adding;

    private bool _reconnecting;

    // Ends the requests that are still running when the page goes or moves to another project.
    private CancellationTokenSource _page = new();

    /// <summary>The project's id, from the address.</summary>
    [Parameter]
    public string Id { get; set; } = string.Empty;

    [Inject]
    private AutomationLanes Lanes { get; set; } = default!;

    [Inject]
    private LiveProjectFactory Factory { get; set; } = default!;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    [Inject]
    private IShortcutListener Keys { get; set; } = default!;

    [Inject]
    private ClipDragState Drags { get; set; } = default!;
    [Inject]
    private BottomPanelState Panel { get; set; } = default!;

    [Inject]
    private RollDragState RollDrags { get; set; } = default!;

    [Inject]
    private PianoRollKeys RollKeys { get; set; } = default!;
    [Inject]
    private PlaybackService Playback { get; set; } = default!;

    [Inject]
    private EffectsNavigator Effects { get; set; } = default!;

    // Red means recording alone, so a connection that is lost or a project that cannot be followed is a caution, never an alarm.
    private StatusDot Dot => _live is { Ready: true } ? StatusDot.Go : StatusDot.Caution;

    private string StateText => _live?.Problem ?? _live switch
    {
        { Ready: true } => "Connected",
        { State: HubState.Connected } => "Joining the project: edits are paused",
        { State: HubState.Reconnecting } => "Reconnecting: edits are paused",
        { State: HubState.Disconnected } => "Disconnected: edits are paused",
        _ => "Connecting",
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Keys.Stop();
        await CloseAsync();
        _page.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_opened != Id)
        {
            _opened = Id;
            await OpenAsync();
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && OperatingSystem.IsBrowser())
        {
            Keys.Listen(Press);
        }
    }

    private async Task OpenAsync()
    {
        await CloseAsync();
        _page = new CancellationTokenSource();
        _failure = null;
        Selection.Clear();
        Panel.Reset();
        LiveProject live = Factory.Create();
        _live = live;
        _editor = new SessionEditor(live);
        _midi = new MidiEditor(live);
        _effects = new EffectEditor(live, _midi.SendAsync);
        _beats = new BeatEditor(live);
        _automation = new AutomationEditor(live);
        live.Changed += OnLiveChanged;
        Playback.Follow(live);
        Rebuild();
        try
        {
            await live.OpenAsync(Id, _page.Token);
        }
        catch (OperationCanceledException) when (_page.IsCancellationRequested)
        {
            // The page went, or moved to another project, while this one was opening: nothing is left to show it to.
            return;
        }
        catch (Exception exception)
        {
            // Whatever the failure was (a refusal, a server that is down, a socket that never opened), the page says so and offers to retry.
            if (ReferenceEquals(_live, live))
            {
                _failure = FailureText.Of(exception);
                await CloseAsync();
            }
        }
        Rebuild();
    }

    private async Task CloseAsync()
    {
        await _page.CancelAsync();
        if (_live is { } live)
        {
            live.Changed -= OnLiveChanged;
            Playback.Unfollow();
            _live = null;
            _editor = null;
            _midi = null;
            _effects = null;
            _beats = null;
            await live.DisposeAsync();
        }
    }

    // After the connection gave up there is no reconnecting by itself; this starts over on a new one and joins again.
    private async Task ReconnectAsync()
    {
        if (_live is not { } live || _reconnecting)
        {
            return;
        }
        _reconnecting = true;
        try
        {
            await live.ReconnectAsync(_page.Token);
        }
        catch (OperationCanceledException) when (_page.IsCancellationRequested)
        {
            // The page went while it was connecting.
        }
        catch (Exception exception)
        {
            Toasts.Show(ToastKind.Error, FailureText.Of(exception));
        }
        finally
        {
            _reconnecting = false;
            Rebuild();
            await InvokeAsync(StateHasChanged);
        }
    }

    private void OnLiveChanged()
    {
        Rebuild();
        _ = InvokeAsync(StateHasChanged);
    }

    // New actions whenever the state changes, so the screen sees that it may edit, or why it may not.
    private void Rebuild()
    {
        bool live = _live is { Ready: true };
        string reason = _live switch
        {
            { Problem: { } problem } => problem,
            { State: HubState.Reconnecting } => "Reconnecting to the server.",
            { State: HubState.Connected } => "Joining the project.",
            { State: HubState.Disconnected } => "Not connected to the server.",
            _ => "Connecting to the server.",
        };
        _actions = !live
            ? SessionActions.ReadOnly(reason, _live is { CanReconnect: true } && !_reconnecting ? ReconnectAsync : null)
            : new SessionActions
            {
                CanEdit = true,
                AddTrack = () => AddTrackAsync(TrackKind.Audio),
                AddInstrumentTrack = () => AddTrackAsync(TrackKind.Instrument),
                Midi = MidiActions.For(_midi!, ShowAsync),
                Effects = EffectActions.For(_effects!, ShowAsync),
                Beats = BeatActions.For(_beats!, ShowAsync),
                Automation = AutomationActions.For(_automation!, ShowAsync, () => _live?.Document),
                RenameTrack = BeginRename,
                ToggleMute = id => ToggleAsync(id, track => _editor!.SetMutedAsync(id, !track.Muted)),
                ToggleSolo = id => ToggleAsync(id, track => _editor!.SetSoloedAsync(id, !track.Soloed)),
                AddClip = AddClipAsync,
                MoveClip = move => ShowAsync(_editor!.MoveClipAsync(move.Clip, move.Start, move.Track)),
                SetMetronome = on => ShowAsync(_editor!.SetMetronomeAsync(on)),
                Undo = () => HistoryAsync(_editor!.UndoAsync(), "undo"),
                Redo = () => HistoryAsync(_editor!.RedoAsync(), "redo"),
            };
    }

    private async Task AddTrackAsync(TrackKind kind)
    {
        // A second press while the first is on its way would add a second track.
        if (_adding)
        {
            return;
        }
        _adding = true;
        try
        {
            EditResult result = await ShowAsync(_editor!.AddTrackAsync(kind));
            if (result.Succeeded && result.Created.Count > 0)
            {
                Selection.SelectTrack(TrackId.From(result.Created[0]));
            }
        }
        finally
        {
            _adding = false;
        }
    }

    private async Task AddMidiClipAsync(TrackId track)
    {
        EditResult result = await _actions.Midi.AddClip(track, null);
        if (result.Succeeded && result.Created.Count > 0)
        {
            Selection.SelectClip(track, ClipId.From(result.Created[0]));
        }
    }

    // A file added to the lanes is the selection afterwards, so the keyboard can carry on from it.
    private async Task AddClipAsync(MediaPlacement placement)
    {
        EditResult result = await ShowAsync(_editor!.AddClipAsync(placement));
        if (result.Succeeded && result.Created.Count > 0)
        {
            Selection.SelectClip(placement.Track, ClipId.From(result.Created[0]));
        }
    }

    private Task ToggleAsync(TrackId id, Func<Track, Task<EditResult>> edit)
    {
        Track? track = _live?.Document?.Tracks.FirstOrDefault(candidate => candidate.Id == id);
        return track is null ? Task.CompletedTask : ShowAsync(edit(track));
    }

    private async Task HistoryAsync(Task<EditResult> step, string what)
    {
        EditResult result = await ShowAsync(step);
        if (result.Succeeded && !result.Changed)
        {
            Toasts.Show(ToastKind.Info, $"There is nothing to {what}.");
        }
    }

    // An edit the server refused says why; a refusal is an ordinary answer, not a failure of the page.
    private async Task<EditResult> ShowAsync(Task<EditResult> edit)
    {
        EditResult result = await edit;
        if (!result.Succeeded && result.Refusal is { } reason)
        {
            Toasts.Show(ToastKind.Error, reason);
        }
        return result;
    }

    private Task BeginRename(TrackId id)
    {
        Track? track = _live?.Document?.Tracks.FirstOrDefault(candidate => candidate.Id == id);
        if (track is not null)
        {
            _renaming = id;
            _name = track.Name;
            _renameError = null;
            Selection.SelectTrack(id);
        }
        return Task.CompletedTask;
    }

    private void EndRename(bool open)
    {
        if (!open)
        {
            _renaming = null;
        }
    }

    private async Task RenameAsync()
    {
        if (_renaming is not { } id)
        {
            return;
        }
        _busy = true;
        EditResult result = await _editor!.RenameTrackAsync(id, _name.Trim());
        _busy = false;
        if (_renaming != id)
        {
            // The dialog was closed while the request was on its way: its answer has no dialog to show it in, so it is a toast.
            if (!result.Succeeded && result.Refusal is { } refusal)
            {
                Toasts.Show(ToastKind.Error, refusal);
            }
            return;
        }
        _renameError = result.Refusal;
        if (result.Succeeded)
        {
            _renaming = null;
        }
    }

    // A key press from anywhere on the page but a text field or a dialog; true means it was a shortcut and is handled.
    // Playing does not edit the project, so the transport keys work while the project is read-only too.
    private bool TransportPress(KeyChord chord)
    {
        if (chord.Repeat && IsTransportKey(chord))
        {
            // A key held down is one press: the repeats are taken and do nothing.
            return true;
        }
        if (Shortcuts.PlayPause.Matches(chord))
        {
            _ = InvokeAsync(Playback.TogglePlayAsync);
        }
        else if (Shortcuts.Stop.Matches(chord))
        {
            Playback.Stop();
        }
        else if (Shortcuts.ToStart.Matches(chord))
        {
            Playback.Seek(0);
        }
        else if (Shortcuts.ToEnd.Matches(chord))
        {
            Playback.Seek(Playback.ContentEnd);
        }
        else if (Shortcuts.SeekBack.Matches(chord))
        {
            Playback.Seek(Playback.Position - Playback.BarSeconds);
        }
        else if (Shortcuts.SeekForward.Matches(chord))
        {
            Playback.Seek(Playback.Position + Playback.BarSeconds);
        }
        else if (Shortcuts.ToggleLoop.Matches(chord))
        {
            Playback.SetLoop(!Playback.Looping);
        }
        else if (Shortcuts.ToggleClick.Matches(chord) && _live?.Document is { } document && _actions.CanEdit)
        {
            _ = InvokeAsync(() => _actions.SetMetronome(!document.Metronome));
        }
        else
        {
            return false;
        }
        return true;
    }

    private static bool IsTransportKey(KeyChord chord) =>
        Shortcuts.PlayPause.Matches(chord) || Shortcuts.Stop.Matches(chord) || Shortcuts.ToStart.Matches(chord)
        || Shortcuts.ToEnd.Matches(chord) || Shortcuts.SeekBack.Matches(chord) || Shortcuts.SeekForward.Matches(chord)
        || Shortcuts.ToggleLoop.Matches(chord) || Shortcuts.ToggleClick.Matches(chord);

    private bool Press(KeyChord chord)
    {
        if (RollKeys.Route(chord, TransportPress))
        {
            return true;
        }
        if (!_actions.CanEdit)
        {
            return false;
        }
        bool history = Shortcuts.Undo.Matches(chord) || Shortcuts.Redo.Matches(chord) || Shortcuts.RedoAlternate.Matches(chord);
        if (history && (Drags.Current is not null || RollDrags.Busy || Lanes.HoldsHistory))
        {
            // A press is down on a clip or a point, or an edit of the roll is on its way: undoing now would take back a step under it.
            // So history waits.
            return true;
        }
        if (Shortcuts.Undo.Matches(chord))
        {
            _ = InvokeAsync(() => _actions.Undo());
        }
        else if (Shortcuts.Redo.Matches(chord) || Shortcuts.RedoAlternate.Matches(chord))
        {
            _ = InvokeAsync(() => _actions.Redo());
        }
        else if (Shortcuts.AddMidiClip.Matches(chord) && Drags.Current is null && !RollDrags.Busy
            && Selection.Track is { } target
            && _live?.Document?.Tracks.Any(candidate => candidate.Id == target && candidate.Kind == TrackKind.Instrument) == true)
        {
            _ = InvokeAsync(() => AddMidiClipAsync(target));
        }
        else if ((Shortcuts.MidiClipLonger.Matches(chord) || Shortcuts.MidiClipShorter.Matches(chord))
            && Drags.Current is null && !RollDrags.Busy && Selection.Clip is { } open
            && _live?.Document is { } document && MidiClipPlan.Find(document, open) is not null)
        {
            int bars = Shortcuts.MidiClipLonger.Matches(chord) ? 1 : -1;
            _ = InvokeAsync(() => _actions.Midi.StepLength(open, bars));
        }
        else if (Shortcuts.AutomationToggle.Matches(chord) && Selection.Track is { } chosen && _actions.CanEdit
            && _live?.Document?.Tracks.FirstOrDefault(track => track.Id == chosen) is { } lanes)
        {
            _ = InvokeAsync(() => _actions.Automation.SetView(lanes.Id, !lanes.View.AutomationVisible, null));
        }
        else if (Shortcuts.OpenEffects.Matches(chord) && Selection.Track is { } selected)
        {
            _ = InvokeAsync(() => Effects.Open(selected, null));
        }
        else if (Shortcuts.RenameTrack.Matches(chord) && Selection.Track is { } track)
        {
            _ = InvokeAsync(async () =>
            {
                await BeginRename(track);
                StateHasChanged();
            });
        }
        else
        {
            return false;
        }
        return true;
    }
}
