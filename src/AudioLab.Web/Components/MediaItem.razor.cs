using AudioLab.Core.Model;
using AudioLab.Web.Media;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue media item: a file of the library with its length, format and size, or an upload on its way with its progress.</summary>
public sealed partial class MediaItem
{
    /// <summary>The file, for a row of the library.</summary>
    [Parameter]
    public MediaSummary? File { get; set; }

    /// <summary>The upload, for a row that is still on its way or was refused.</summary>
    [Parameter]
    public UploadJob? Job { get; set; }

    /// <summary>Whether this file is the selected one.</summary>
    [Parameter]
    public bool Selected { get; set; }

    /// <summary>Whether this is the one row of the list that is a tab stop; the arrow keys move it.</summary>
    [Parameter]
    public bool TabStop { get; set; }

    /// <summary>The time now, which an upload that waits for the server measures its wait against.</summary>
    [Parameter]
    public DateTimeOffset Now { get; set; }

    /// <summary>Raised with the file when the row is pressed or activated.</summary>
    [Parameter]
    public EventCallback<MediaSummary> OnSelect { get; set; }

    /// <summary>Raised with the file and the key for a key the list handles, such as an arrow.</summary>
    [Parameter]
    public EventCallback<(MediaSummary File, KeyboardEventArgs Key)> OnKey { get; set; }

    /// <summary>Raised with the file and the press when the primary button goes down on it, which may begin a drag onto the lanes.</summary>
    [Parameter]
    public EventCallback<(MediaSummary File, PointerEventArgs Press)> OnPointerDown { get; set; }

    /// <summary>Raised with the file when its delete control is pressed.</summary>
    [Parameter]
    public EventCallback<MediaSummary> OnDelete { get; set; }

    /// <summary>Raised with the upload when it is cancelled, or when a refused one is dismissed.</summary>
    [Parameter]
    public EventCallback<UploadJob> OnCancel { get; set; }

    /// <summary>Raised with the upload when a refused one is to be sent again.</summary>
    [Parameter]
    public EventCallback<UploadJob> OnRetry { get; set; }

    /// <summary>The id of a file's row, so the list can move the focus to it.</summary>
    public static string IdOf(string file) => $"media-{file}";

    private string ElementId => IdOf(File!.Id);

    private string Description => $"{File!.Name}, {MediaText.Length(File.Duration)} long, {MediaText.Format(File)}, {MediaText.Size(File.Bytes)}";

    private string Hint => $"{File!.Name}. Drag it onto an audio track, or press {Shortcuts.AddMedia.Display} to add it at the playhead.";

    private string StateName => Job!.State.ToString().ToLowerInvariant();

    private double? ProgressFraction => Job!.State == UploadState.Sending ? null : Job.State == UploadState.Queued ? 0 : Job.Fraction;

    private string ProgressText => Job!.State switch
    {
        UploadState.Queued => "Waiting for its turn",
        UploadState.Reading => $"Reading {(int)(Job.Fraction * 100)}%",
        _ => $"Waiting for the server, {MediaText.Elapsed(Now - (Job.SendingSince ?? Now))}",
    };

    private Task SelectAsync() => OnSelect.InvokeAsync(File!);

    private Task PressAsync(PointerEventArgs press) => press.Button == 0 ? OnPointerDown.InvokeAsync((File!, press)) : Task.CompletedTask;

    private Task KeyAsync(KeyboardEventArgs key) => OnKey.InvokeAsync((File!, key));

    private Task DeleteAsync() => OnDelete.InvokeAsync(File!);

    private Task CancelAsync() => OnCancel.InvokeAsync(Job!);

    private Task RetryAsync() => OnRetry.InvokeAsync(Job!);
}
