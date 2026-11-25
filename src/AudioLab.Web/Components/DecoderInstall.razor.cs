using System.Net;
using AudioLab.Core.Api.Decoder;
using AudioLab.Web.Interop;
using AudioLab.Web.Screens;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The Media tab's install of the optional audio decoder (D101). The button is offered only when the server has a build
/// for this platform, lets users install, and has none installed. The download starts only when the user confirms the
/// version, source, size and checksum the dialog shows. Once the install is running the page shows its progress, and a
/// reload shows the same progress again.</summary>
public sealed partial class DecoderInstall : IDisposable
{
    // The install runs on the server; the page asks how it is going this often until it has finished.
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);

    private readonly CancellationTokenSource _page = new();

    private DecoderStatus? _status;

    private bool _open;

    private bool _busy;

    private string? _message;

    [Inject]
    private DecoderApi Api { get; set; } = default!;

    [Inject]
    private DecoderState Decoder { get; set; } = default!;

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    /// <summary>What the confirmation says will be installed, and from where.</summary>
    private string DialogText
    {
        get
        {
            if (_status?.Package is not { } package)
            {
                return string.Empty;
            }
            return $"Version {package.Version} from {new Uri(package.SourceUrl).Host}, {UploadChecks.Size(package.Bytes)}. "
                + $"SHA-256 {package.Sha256}. "
                + "The file is downloaded only when you confirm it, checked against this checksum before it is unpacked, "
                + "and kept in the server's data folder. "
                + "This build of ffmpeg is licensed under the GNU GPL version 3.";
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            DecoderStatus status = await Api.StatusAsync(_page.Token);
            _status = status;
            Decoder.Set(status.Installed);
            if (IsRunning(status.Phase))
            {
                // A reload during an install: the install is still running on the server, so its progress is shown, not a refusal later.
                _busy = true;
                _ = FollowAsync(status);
            }
        }
        catch (ApiException)
        {
            // Without a status the button stays hidden; the media list reports its own failures.
            _status = null;
        }
        catch (HttpRequestException)
        {
            _status = null;
        }
    }

    private void Open()
    {
        _message = null;
        _open = true;
    }

    // The confirmation can be dismissed at any time, since an install starts only from its Install button.
    private void SetOpen(bool open) => _open = open;

    private async Task ConfirmAsync()
    {
        if (_status?.Package is not { } package || _busy)
        {
            return;
        }
        _open = false;
        _busy = true;
        _message = "Starting the install.";
        try
        {
            DecoderStatus status = await Api.InstallAsync(package, _page.Token);
            await FollowAsync(status);
        }
        catch (ApiException exception) when (exception.Status == HttpStatusCode.Conflict)
        {
            // Another tab or window started this install: follow its progress rather than report a refusal.
            await FollowRunningAsync(exception.Message);
        }
        catch (ApiException exception)
        {
            _busy = false;
            _message = exception.Message;
        }
        catch (HttpRequestException)
        {
            _busy = false;
            _message = "The server could not be reached, so the install did not start. Try again.";
        }
    }

    private async Task FollowRunningAsync(string refusal)
    {
        try
        {
            DecoderStatus status = await Api.StatusAsync(_page.Token);
            if (IsRunning(status.Phase))
            {
                await FollowAsync(status);
                return;
            }
            _busy = false;
            _message = refusal;
        }
        catch (ApiException)
        {
            _busy = false;
            _message = refusal;
        }
        catch (HttpRequestException)
        {
            _busy = false;
            _message = refusal;
        }
    }

    // Shows the install's progress until it ends. The progress says the install keeps going when the page is left, which is true:
    // the download belongs to the server, and nothing here cancels it.
    private async Task FollowAsync(DecoderStatus status)
    {
        try
        {
            _status = status;
            while (IsRunning(status.Phase))
            {
                _message = Progress(status);
                StateHasChanged();
                await Task.Delay(_pollInterval, _page.Token);
                status = await Api.StatusAsync(_page.Token);
                _status = status;
            }
            if (status.Phase == DecoderPhases.Failed)
            {
                _message = $"The install failed. {status.Reason}";
            }
            else if (status.Installed)
            {
                _message = $"Audio decoder {status.InstalledVersion} is installed. Compressed audio can now be added.";
                Focus.Focus(MediaPanel.AddId);
            }
            Decoder.Set(status.Installed);
        }
        catch (ApiException exception)
        {
            _message = $"The install's progress could not be read: {exception.Message} Reload the page to see its state.";
        }
        catch (HttpRequestException)
        {
            _message = "The server could not be reached, so the install's progress is not known. Reload the page to see its state.";
        }
        catch (OperationCanceledException) when (_page.IsCancellationRequested)
        {
            // The page is closing; the install itself carries on in the server.
            return;
        }
        finally
        {
            _busy = false;
            StateHasChanged();
        }
    }

    private static bool IsRunning(string phase) =>
        phase is DecoderPhases.Downloading or DecoderPhases.Verifying or DecoderPhases.Extracting;

    private static string Progress(DecoderStatus status)
    {
        const string KeepsGoing = " It keeps going if you leave this page.";
        if (status.Phase == DecoderPhases.Downloading && status.Total > 0)
        {
            return $"Downloading {UploadChecks.Size(status.Received)} of {UploadChecks.Size(status.Total)}.{KeepsGoing}";
        }
        return status.Phase switch
        {
            DecoderPhases.Downloading => $"Downloading.{KeepsGoing}",
            DecoderPhases.Verifying => $"Checking the download against its checksum.{KeepsGoing}",
            DecoderPhases.Extracting => $"Installing the decoder.{KeepsGoing}",
            _ => "Starting the install.",
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _page.Cancel();
        _page.Dispose();
    }
}
