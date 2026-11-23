using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Media;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Screens;

/// <summary>The media tab of the bottom panel: the user's WAV files with their length, format and size, uploads on their way, and delete.</summary>
public sealed partial class MediaPanel : IDisposable
{
    private const string PanelId = "media-panel";

    private const string ListId = "media-list";

    // Progress arrives many times a second; the list is drawn at most this often, so a busy upload never makes the page busy.
    private static readonly TimeSpan _drawInterval = TimeSpan.FromMilliseconds(100);

    private readonly CancellationTokenSource _panel = new();

    private ITimer? _tick;

    private string? _selected;

    private string? _focus;

    private int _shown = MediaListView.PageSize;

    private MediaListView? _view;

    private IReadOnlyList<MediaSummary>? _viewFiles;

    private string? _viewSelected;

    private int _viewShown;

    private bool _drawQueued;

    private bool _guarded;

    private MediaSummary? _target;

    private bool _deleting;

    private string? _error;

    private CancellationTokenSource? _request;

    /// <summary>The document, which the lanes a file is dragged onto are laid out from.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project, and why not when it cannot.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    [Inject]
    private MediaLibrary Library { get; set; } = default!;

    [Inject]
    private MediaDragger Dragger { get; set; } = default!;

    [Inject]
    private MediaAdder Adder { get; set; } = default!;

    [Inject]
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private SessionPlayhead Playhead { get; set; } = default!;

    [Inject]
    private IElementClick Click { get; set; } = default!;

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    [Inject]
    private IKeyGuard Guard { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    private string? Blocked => Actions.CanEdit ? null : Actions.Reason;

    private DateTimeOffset Now => Time.GetUtcNow();

    private bool Loading => Library.State is LibraryState.NotLoaded or LibraryState.Loading;

    private bool Empty => Library.Files.Count == 0 && Library.Uploads.Count == 0;

    // A panel has one primary button: an empty or failed panel gives it to the call to action in its body.
    private ButtonVariant AddVariant => Empty ? ButtonVariant.Default : ButtonVariant.Primary;

    // Worked out again only when the list, the selection or the page size changed, so a draw that changed none of them costs nothing.
    private MediaListView View
    {
        get
        {
            if (_view is null || !ReferenceEquals(_viewFiles, Library.Files) || _viewSelected != _selected || _viewShown != _shown)
            {
                _view = new MediaListView(Library.Files, _selected, _shown);
                _viewFiles = Library.Files;
                _viewSelected = _selected;
                _viewShown = _shown;
            }
            return _view;
        }
    }

    private string CountText => Library.State switch
    {
        LibraryState.Ready or LibraryState.Failed when Library.Files.Count == 1 => "1 file",
        LibraryState.Ready or LibraryState.Failed => $"{Library.Files.Count} files",
        _ => string.Empty,
    };

    private string RefusedText => Library.Refused == 1
        ? "One stored media file was left out because it is damaged."
        : $"{Library.Refused} stored media files were left out because they are damaged.";

    private string DeleteText => _target is null
        ? string.Empty
        : $"Deleting \"{_target.Name}\" removes the file from your library for good. This cannot be undone.";

    /// <inheritdoc />
    public void Dispose()
    {
        Library.Changed -= OnChanged;
        Dragger.Cancel();
        _tick?.Dispose();
        _request?.Cancel();
        _panel.Cancel();
        _panel.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Library.Changed += OnChanged;
        _tick = Time.CreateTimer(_ => OnTick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        // Opening the tab reads the list again, so a file added or deleted from another window is there.
        await Library.RefreshAsync(_panel.Token);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (Library.Files.Count + Library.Uploads.Count == 0)
        {
            _guarded = false;
        }
        else if (!_guarded && OperatingSystem.IsBrowser())
        {
            _guarded = true;
            Guard.PreventNavigationKeys(ListId, "button");
        }
        if (_focus is not null)
        {
            Focus.Focus(_focus);
            _focus = null;
        }
    }

    // A change is drawn once the interval since the last draw has passed, however many changes come in it.
    private void OnChanged()
    {
        if (_drawQueued)
        {
            return;
        }
        _drawQueued = true;
        _ = InvokeAsync(async () =>
        {
            await Task.Delay(_drawInterval, Time);
            _drawQueued = false;
            StateHasChanged();
        });
    }

    // The wait for the server has a clock beside it, which moves once a second while one is running.
    private void OnTick()
    {
        if (Library.Uploads.Any(job => job.State == UploadState.Sending))
        {
            OnChanged();
        }
    }

    private void Choose() => Click.Click(FilePicker.ChooserId);

    private Task ReloadAsync() => Library.LoadAsync(_panel.Token);

    private void Select(MediaSummary file) => _selected = file.Id;

    private void CancelUpload(UploadJob job) => Library.Cancel(job.Id);

    private void RetryUpload(UploadJob job) => Library.Retry(job.Id);

    private void ShowMore()
    {
        int first = _shown;
        _shown += MediaListView.PageSize;
        if (first < Library.Files.Count)
        {
            _focus = MediaItem.IdOf(Library.Files[first].Id);
        }
    }

    // A press on a file selects it and may become a drag onto the lanes; nothing is sent until the release. Where the project cannot be
    // edited the drag still follows the pointer and says why, so a press that does nothing is not a silent one.
    private void PressAsync((MediaSummary File, PointerEventArgs Press) down)
    {
        Select(down.File);
        Dragger.Begin(down.File, () => Document, Actions.AddClip, down.Press.ClientX, down.Press.ClientY, Blocked);
    }

    // The keyboard path to the same edit, and the button: the selected audio track, at the playhead or after the selected clip.
    private Task AddAsync(MediaSummary file)
    {
        return Adder.AddAsync(file, Document, Selection.Track, Selection.Clip, Playhead.Seconds, Actions.CanEdit, Actions.Reason, Actions.AddClip);
    }

    private Task KeyAsync((MediaSummary File, KeyboardEventArgs Key) press)
    {
        if (Shortcuts.AddMedia.Matches(press.Key))
        {
            // A key held down repeats, and one add is enough.
            return press.Key.Repeat ? Task.CompletedTask : AddAsync(press.File);
        }
        KeyStep(press);
        return Task.CompletedTask;
    }

    private void KeyStep((MediaSummary File, KeyboardEventArgs Key) press)
    {
        if (Shortcuts.DeleteMedia.Matches(press.Key))
        {
            BeginDelete(press.File);
            return;
        }
        MediaStep? step = Step(press.Key);
        if (step is MediaStep to && View.Go(press.File.Id, to) is { } go)
        {
            _shown = Math.Max(_shown, go.Draw);
            _selected = go.Target.Id;
            _focus = MediaItem.IdOf(_selected);
        }
    }

    private static MediaStep? Step(KeyboardEventArgs key)
    {
        if (Shortcuts.MediaNext.Matches(key))
        {
            return MediaStep.Next;
        }
        if (Shortcuts.MediaPrevious.Matches(key))
        {
            return MediaStep.Previous;
        }
        if (Shortcuts.MediaFirst.Matches(key))
        {
            return MediaStep.First;
        }
        return Shortcuts.MediaLast.Matches(key) ? MediaStep.Last : null;
    }

    private void BeginDelete(MediaSummary file)
    {
        _error = null;
        _target = file;
    }

    private void EndDelete(bool open)
    {
        if (!open)
        {
            _request?.Cancel();
            _target = null;
        }
    }

    private async Task DeleteAsync()
    {
        if (_target is null || _deleting)
        {
            return;
        }
        MediaSummary target = _target;
        int at = View.IndexOf(target.Id);
        using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(_panel.Token);
        _request = request;
        _deleting = true;
        _error = null;
        try
        {
            await Library.DeleteAsync(target, request.Token);
            _focus = NeighbourOf(at);
            _target = null;
        }
        catch (OperationCanceledException)
        {
            // Cancel or Escape ended the request; the dialog is already closing. A timeout is not this: it is a failure, shown below.
        }
        catch (Exception exception)
        {
            _error = FailureText.Of(exception);
        }
        finally
        {
            _request = null;
            _deleting = false;
        }
    }

    // After a delete the focus goes to the file that took its place, or the one before it, or to the panel when none is left.
    private string NeighbourOf(int deleted)
    {
        IReadOnlyList<MediaSummary> files = Library.Files;
        return files.Count == 0 ? PanelId : MediaItem.IdOf(files[Math.Clamp(deleted, 0, files.Count - 1)].Id);
    }
}
