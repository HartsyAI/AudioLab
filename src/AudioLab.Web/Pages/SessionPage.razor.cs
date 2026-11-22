using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Pages;

/// <summary>The route of one open project: it joins over the hub, draws every revision it is sent, and makes the edits the screen offers.</summary>
public sealed partial class SessionPage
{
    private LiveProject? _live;

    private SessionEditor? _editor;

    private SessionActions _actions = SessionActions.ReadOnly("Opening the project.");

    private string? _opened;

    private string? _failure;

    private TrackId? _renaming;

    private string _name = string.Empty;

    private string? _renameError;

    private bool _busy;

    /// <summary>The project's id, from the address.</summary>
    [Parameter]
    public string Id { get; set; } = string.Empty;

    [Inject]
    private LiveProjectFactory Factory { get; set; } = default!;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    [Inject]
    private IShortcutListener Keys { get; set; } = default!;

    private StatusDot Dot => _live is not { Problem: null } ? StatusDot.Record : _live.State switch
    {
        HubState.Connected => StatusDot.Go,
        HubState.Disconnected => StatusDot.Record,
        _ => StatusDot.Caution,
    };

    private string StateText => _live?.Problem ?? _live?.State switch
    {
        HubState.Connected => "Connected",
        HubState.Connecting => "Connecting",
        HubState.Reconnecting => "Reconnecting: edits are paused",
        _ => "Disconnected",
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Keys.Stop();
        await CloseAsync();
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
        _failure = null;
        Selection.Clear();
        _live = Factory.Create();
        _editor = new SessionEditor(_live);
        _live.Changed += OnLiveChanged;
        Rebuild();
        try
        {
            await _live.OpenAsync(Id);
        }
        catch (Exception exception) when (exception is HubException or HttpRequestException or InvalidOperationException)
        {
            _failure = exception is HubException { Message.Length: > 0 }
                ? exception.Message
                : "The server did not answer. Check that AudioLab is still running.";
            await CloseAsync();
        }
        Rebuild();
    }

    private async Task CloseAsync()
    {
        if (_live is { } live)
        {
            live.Changed -= OnLiveChanged;
            _live = null;
            await live.DisposeAsync();
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
        bool live = _live is { Document: not null, Problem: null, State: HubState.Connected };
        string reason = _live switch
        {
            { Problem: { } problem } => problem,
            { State: HubState.Reconnecting } => "Reconnecting to the server.",
            { State: HubState.Connecting } or null => "Connecting to the server.",
            _ => "Not connected to the server.",
        };
        _actions = !live
            ? SessionActions.ReadOnly(reason)
            : new SessionActions
            {
                CanEdit = true,
                AddTrack = AddTrackAsync,
                RenameTrack = BeginRename,
                ToggleMute = id => ToggleAsync(id, track => _editor!.SetMutedAsync(id, !track.Muted)),
                ToggleSolo = id => ToggleAsync(id, track => _editor!.SetSoloedAsync(id, !track.Soloed)),
                MoveClip = move => ShowAsync(_editor!.MoveClipAsync(move.Clip, move.Start, move.Track)),
                Undo = () => HistoryAsync(_editor!.UndoAsync(), "undo"),
                Redo = () => HistoryAsync(_editor!.RedoAsync(), "redo"),
            };
    }

    private async Task AddTrackAsync()
    {
        EditResult result = await ShowAsync(_editor!.AddTrackAsync());
        if (result.Succeeded && result.Created.Count > 0)
        {
            Selection.SelectTrack(TrackId.From(result.Created[0]));
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
        _renameError = result.Refusal;
        if (result.Succeeded)
        {
            _renaming = null;
        }
    }

    // A key press from anywhere on the page but a text field or a dialog; true means it was a shortcut and is handled.
    private bool Press(KeyChord chord)
    {
        if (!_actions.CanEdit)
        {
            return false;
        }
        if (Shortcuts.Undo.Matches(chord))
        {
            _ = InvokeAsync(() => _actions.Undo());
        }
        else if (Shortcuts.Redo.Matches(chord) || Shortcuts.RedoAlternate.Matches(chord))
        {
            _ = InvokeAsync(() => _actions.Redo());
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
