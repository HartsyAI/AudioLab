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
    private readonly CancellationTokenSource _panel = new();

    private ITimer? _tick;

    private string? _selected;

    private string? _focus;

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
    private SessionSelection Selection { get; set; } = default!;

    [Inject]
    private SessionPlayhead Playhead { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    [Inject]
    private IElementClick Click { get; set; } = default!;

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    private DateTimeOffset Now => Time.GetUtcNow();

    private bool Loading => Library.State is LibraryState.NotLoaded or LibraryState.Loading;

    private bool Empty => Library.Files.Count == 0 && Library.Uploads.Count == 0;

    // A panel has one primary button: an empty or failed panel gives it to the call to action in its body.
    private ButtonVariant AddVariant => Empty ? ButtonVariant.Default : ButtonVariant.Primary;

    private string? TabStopId => Library.Files.Any(file => file.Id == _selected) ? _selected : Library.Files.FirstOrDefault()?.Id;

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
        if (Library.State is LibraryState.NotLoaded or LibraryState.Failed)
        {
            await Library.LoadAsync(_panel.Token);
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (_focus is not null)
        {
            Focus.Focus(MediaItem.IdOf(_focus));
            _focus = null;
        }
    }

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    // The wait for the server has a clock beside it, which moves once a second while one is running.
    private void OnTick()
    {
        if (Library.Uploads.Any(job => job.State == UploadState.Sending))
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void Choose() => Click.Click(FilePicker.ChooserId);

    private Task ReloadAsync() => Library.LoadAsync(_panel.Token);

    private void Select(MediaSummary file) => _selected = file.Id;

    private void CancelUpload(UploadJob job) => Library.Cancel(job.Id);

    private void RetryUpload(UploadJob job) => Library.Retry(job.Id);

    // A press on a file selects it and, when the project can be edited, may become a drag onto the lanes; nothing is sent until the release.
    private void PressAsync((MediaSummary File, PointerEventArgs Press) down)
    {
        Select(down.File);
        if (Actions.CanEdit)
        {
            Dragger.Begin(down.File, Document, down.Press.ClientX, down.Press.ClientY, Actions.AddClip);
        }
    }

    // The keyboard path to the same edit: the selected audio track, at the playhead or after the selected clip.
    private Task AddAtInsertionPointAsync(MediaSummary file)
    {
        if (!Actions.CanEdit)
        {
            Toasts.Show(ToastKind.Warning, $"The file was not added. {Actions.Reason}");
            return Task.CompletedTask;
        }
        (MediaPlacement? placement, string? refusal) = ClipInsertion.For(Document, file, Selection.Track, Selection.Clip, Playhead.Seconds);
        if (placement is not { } where)
        {
            Toasts.Show(ToastKind.Error, refusal ?? "The file could not be added.");
            return Task.CompletedTask;
        }
        return Actions.AddClip(where);
    }

    private Task KeyAsync((MediaSummary File, KeyboardEventArgs Key) press)
    {
        if (Shortcuts.AddMedia.Matches(press.Key))
        {
            return AddAtInsertionPointAsync(press.File);
        }
        if (Shortcuts.DeleteMedia.Matches(press.Key))
        {
            BeginDelete(press.File);
            return Task.CompletedTask;
        }
        IReadOnlyList<MediaSummary> files = Library.Files;
        int at = files.ToList().FindIndex(file => file.Id == press.File.Id);
        int? target = press.Key.Key switch
        {
            "ArrowDown" => Math.Min(at + 1, files.Count - 1),
            "ArrowUp" => Math.Max(at - 1, 0),
            "Home" => 0,
            "End" => files.Count - 1,
            _ => null,
        };
        if (target is int next && at >= 0)
        {
            _selected = files[next].Id;
            _focus = _selected;
        }
        return Task.CompletedTask;
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
        using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(_panel.Token);
        _request = request;
        _deleting = true;
        _error = null;
        try
        {
            await Library.DeleteAsync(_target, request.Token);
            _focus = Library.Files.FirstOrDefault()?.Id;
            _target = null;
        }
        catch (OperationCanceledException)
        {
            // Cancel or Escape ended the request; the dialog is already closing.
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
}
