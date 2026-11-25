using AudioLab.Core.Api.Decoder;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The Media tab's install of the optional audio decoder (D101). The button shows only when the server has a build for
/// this platform and none is installed. The download starts only when the user confirms the version, source, size and
/// checksum the dialog shows.</summary>
public sealed partial class DecoderInstall : IDisposable
{
    // The install runs on the server; the page asks how it is going this often until it has finished.
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(500);

    private readonly CancellationTokenSource _page = new();

    private DecoderStatus? _status;

    private bool _open;

    private bool _busy;

    private string? _error;

    [Inject]
    private DecoderApi Api { get; set; } = default!;

    /// <summary>The text of the dialog: what will be installed before the user confirms, and the progress while it is installed.</summary>
    private string DialogText
    {
        get
        {
            if (_busy)
            {
                return Progress(_status);
            }
            if (_status?.Package is not { } package)
            {
                return string.Empty;
            }
            return $"Version {package.Version} from {new Uri(package.SourceUrl).Host}, {UploadChecks.Size(package.Bytes)}. "
                + $"SHA-256 {package.Sha256}. "
                + "The file is downloaded only when you confirm it, checked against this checksum before it is unpacked, and kept in the server's data folder. "
                + "This build of ffmpeg is licensed under the GNU GPL version 3.";
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _status = await Api.StatusAsync(_page.Token);
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
        _error = null;
        _open = true;
    }

    // A dialog cannot be closed while an install runs, since the progress is shown in it.
    private void SetOpen(bool open)
    {
        if (!_busy)
        {
            _open = open;
        }
    }

    private async Task ConfirmAsync()
    {
        if (_status?.Package is not { } package || _busy)
        {
            return;
        }
        _busy = true;
        _error = null;
        try
        {
            DecoderStatus status = await Api.InstallAsync(package, _page.Token);
            _status = status;
            while (status.Phase is DecoderPhases.Downloading or DecoderPhases.Verifying or DecoderPhases.Extracting)
            {
                StateHasChanged();
                await Task.Delay(_pollInterval, _page.Token);
                status = await Api.StatusAsync(_page.Token);
                _status = status;
            }
            if (status.Phase == DecoderPhases.Failed)
            {
                _error = status.Reason ?? "The install failed, so nothing was installed.";
            }
            else
            {
                _open = false;
            }
        }
        catch (ApiException exception)
        {
            _error = exception.Message;
        }
        catch (HttpRequestException)
        {
            _error = "The server could not be reached. Try again.";
        }
        catch (OperationCanceledException) when (_page.IsCancellationRequested)
        {
            // The page is closing; the install itself carries on in the server.
        }
        finally
        {
            _busy = false;
        }
    }

    private static string Progress(DecoderStatus? status)
    {
        string phase = status?.Phase ?? string.Empty;
        if (phase == DecoderPhases.Downloading && status is not null && status.Total > 0)
        {
            return $"Downloading {UploadChecks.Size(status.Received)} of {UploadChecks.Size(status.Total)}.";
        }
        return phase switch
        {
            DecoderPhases.Downloading => "Downloading.",
            DecoderPhases.Verifying => "Checking the download against its checksum.",
            DecoderPhases.Extracting => "Installing the decoder.",
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
