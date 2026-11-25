using System.IO.Compression;
using System.Security.Cryptography;
using AudioLab.Core.Api.Decoder;

namespace AudioLab.Server.Decoding;

/// <summary>Installs the pinned decoder: downloads the archive the user confirmed, checks its SHA-256, extracts the one binary
/// and checks that too, and reports what it is doing. A binary is run only after <see cref="VerifiedBinary"/> has
/// matched its checksum.</summary>
public sealed class DecoderInstaller
{
    private const long MaxBinaryBytes = 512L * 1024 * 1024;
    private const int BufferBytes = 81920;

    private readonly DecoderOptions _options;
    private readonly DecoderLocation _location;
    private readonly IDecoderDownloader _downloader;
    private readonly DecoderBuild? _build;
    private readonly ILogger<DecoderInstaller> _logger;
    private readonly SemaphoreSlim _running = new(1, 1);
    private readonly Lock _lock = new();
    private string _phase = DecoderPhases.Idle;
    private long _received;
    private string? _reason;
    private (string Path, long Length, DateTime Written)? _statusCheck;
    private bool _statusVerified;

    /// <summary>Creates the installer for the build this platform has, or none when it has none.</summary>
    public DecoderInstaller(
        DecoderOptions options,
        DecoderLocation location,
        IDecoderDownloader downloader,
        DecoderBuild? build,
        ILogger<DecoderInstaller> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _build = build;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Whether a build is pinned for this platform; when it is not, no install is possible and no decode can run.</summary>
    public bool Supported => _build is not null;

    /// <summary>The installed decoder's path, or null when none is installed.</summary>
    /// <remarks>
    /// The binary is hashed on every call. A cache keyed by its size and write time would let a same-size replacement run, and
    /// the hash costs well under a second against the at most two decodes that run at once.
    /// </remarks>
    /// <exception cref="DecoderNotInstalledException">The installed binary does not match its pinned checksum, so it is not run.</exception>
    public string? VerifiedBinary()
    {
        if (_build is null)
        {
            return null;
        }
        string path = _location.Binary(_build.Package.Version, _build.BinaryName);
        if (!File.Exists(path))
        {
            return null;
        }
        if (!string.Equals(HashFile(path), _build.BinarySha256, StringComparison.OrdinalIgnoreCase))
        {
            DecoderLog.Refused(_logger, "the installed binary does not match its pinned checksum");
            throw new DecoderNotInstalledException(
                "The installed audio decoder does not match its pinned checksum, so it will not run. Install it again from the Media tab.");
        }
        return path;
    }

    /// <summary>What the install state is now, for the status route.</summary>
    public DecoderStatus Status()
    {
        string phase;
        long received;
        string? reason;
        lock (_lock)
        {
            phase = _phase;
            received = _received;
            reason = _reason;
        }
        bool installed = StatusSaysInstalled();
        if (!installed && _build is not null && File.Exists(_location.Binary(_build.Package.Version, _build.BinaryName)))
        {
            reason ??= "The installed audio decoder does not match its pinned checksum, so it will not run. Install it again from the Media tab.";
        }
        return new DecoderStatus(
            _build is not null,
            installed,
            installed ? _build!.Package.Version : null,
            phase,
            received,
            _build?.Package.Bytes ?? 0,
            reason,
            _build?.Package,
            _options.InstallEnabled);
    }

    /// <summary>Starts an install of the pinned build when the request names exactly that build; the download runs in the background.</summary>
    public DecoderInstallOutcome Start(DecoderInstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_options.InstallEnabled)
        {
            return DecoderInstallOutcome.Disabled;
        }
        if (_build is null)
        {
            return DecoderInstallOutcome.Unsupported;
        }
        if (!string.Equals(request.Version, _build.Package.Version, StringComparison.Ordinal)
            || !string.Equals(request.Sha256, _build.Package.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return DecoderInstallOutcome.NotConfirmed;
        }
        if (IsInstalled())
        {
            return DecoderInstallOutcome.AlreadyInstalled;
        }
        if (!_running.Wait(0))
        {
            return DecoderInstallOutcome.Busy;
        }
        lock (_lock)
        {
            _phase = DecoderPhases.Downloading;
            _received = 0;
            _reason = null;
        }
        DecoderBuild build = _build;
        _ = Task.Run(() => RunAsync(build));
        return DecoderInstallOutcome.Started;
    }

    private bool IsInstalled()
    {
        try
        {
            return VerifiedBinary() is not null;
        }
        catch (DecoderNotInstalledException)
        {
            // A binary that does not match is replaced by the install, so it counts as not installed here.
            return false;
        }
    }

    // The status is asked on every poll of the page, so an installed binary is hashed once per file state (path, length and write
    // time). A decode never uses this: VerifiedBinary hashes the file itself on every decode.
    private bool StatusSaysInstalled()
    {
        if (_build is null)
        {
            return false;
        }
        string path = _location.Binary(_build.Package.Version, _build.BinaryName);
        if (!File.Exists(path))
        {
            return false;
        }
        FileInfo info = new(path);
        (string Path, long Length, DateTime Written) state = (path, info.Length, info.LastWriteTimeUtc);
        lock (_lock)
        {
            if (_statusCheck is { } known && known == state)
            {
                return _statusVerified;
            }
        }
        bool verified;
        try
        {
            verified = VerifiedBinary() is not null;
        }
        catch (DecoderNotInstalledException)
        {
            verified = false;
        }
        lock (_lock)
        {
            _statusCheck = state;
            _statusVerified = verified;
        }
        return verified;
    }

    // The phase is always left as installed or failed: when the run ends any other way, the finally block fails it, so a status poll
    // never waits forever on a download that is no longer running.
    private async Task RunAsync(DecoderBuild build)
    {
        string archive = Path.Combine(_location.TempFolder, $"{Guid.NewGuid():N}.zip");
        string binary = Path.Combine(_location.TempFolder, $"{Guid.NewGuid():N}.exe");
        using CancellationTokenSource limit = new(_options.InstallTimeout);
        bool completed = false;
        bool reported = false;
        try
        {
            await using (FileStream file = new(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferBytes, FileOptions.Asynchronous))
            {
                long received = await _downloader.DownloadAsync(
                    new Uri(build.Package.SourceUrl),
                    build.Hosts,
                    file,
                    build.Package.Bytes,
                    SetReceived,
                    limit.Token);
                if (received != build.Package.Bytes)
                {
                    throw new DecoderInstallFailedException("The download is not the pinned size, so nothing was installed.");
                }
            }
            SetPhase(DecoderPhases.Verifying);
            if (!string.Equals(HashFile(archive), build.Package.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new DecoderInstallFailedException("The archive does not match its pinned checksum, so nothing was installed.");
            }
            SetPhase(DecoderPhases.Extracting);
            ExtractBinary(archive, binary, build);
            string target = _location.Binary(build.Package.Version, build.BinaryName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Move(binary, target, overwrite: true);
            completed = true;
            SetPhase(DecoderPhases.Installed);
            DecoderLog.Installed(_logger, build.Package.Version);
        }
        catch (Exception exception) when (exception is DecoderInstallFailedException or IOException or HttpRequestException or InvalidDataException
            or UnauthorizedAccessException or OperationCanceledException)
        {
            string reason = exception switch
            {
                DecoderInstallFailedException failed => failed.Message,
                OperationCanceledException when limit.IsCancellationRequested =>
                    "The download took longer than the server allows, so nothing was installed.",
                _ => "The download or the extraction failed, so nothing was installed.",
            };
            DecoderLog.InstallFailed(_logger, exception.GetType().Name);
            Fail(reason);
            reported = true;
        }
        catch (Exception exception) when (LogUnexpected(exception))
        {
            // Never reached: the filter above logs the exception once and lets it pass, so it still ends the run as a failure below.
            throw;
        }
        finally
        {
            if (!completed && !reported)
            {
                // Any other exception ends the run here as a failure, so the phase never stays at downloading.
                Fail("The install stopped unexpectedly, so nothing was installed.");
            }
            // The gate is given back first: an unexpected exception from the deletes below must not leave every later install refused.
            _running.Release();
            TryDelete(archive);
            TryDelete(binary);
        }
    }

    // Only the one named entry is read, and only into the fixed temporary path: no name from the archive is ever a path.
    private static void ExtractBinary(string archive, string binary, DecoderBuild build)
    {
        using ZipArchive zip = ZipFile.OpenRead(archive);
        ZipArchiveEntry? entry = zip.GetEntry(build.EntryName);
        if (entry is null || entry.Length > MaxBinaryBytes)
        {
            throw new DecoderInstallFailedException("The archive does not hold the decoder binary, so nothing was installed.");
        }
        using (Stream source = entry.Open())
        using (FileStream destination = new(binary, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferBytes))
        {
            byte[] buffer = new byte[BufferBytes];
            long total = 0;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > MaxBinaryBytes)
                {
                    throw new DecoderInstallFailedException("The decoder binary is larger than allowed, so nothing was installed.");
                }
                destination.Write(buffer, 0, read);
            }
        }
        if (!string.Equals(HashFile(binary), build.BinarySha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new DecoderInstallFailedException("The decoder binary does not match its pinned checksum, so nothing was installed.");
        }
    }

    // The filter logs the exception's type once, with a fixed message, and returns false so the exception keeps going.
    private bool LogUnexpected(Exception exception)
    {
        DecoderLog.InstallStopped(_logger, exception.GetType().Name);
        return false;
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private void SetPhase(string phase)
    {
        lock (_lock)
        {
            _phase = phase;
        }
    }

    private void Fail(string reason)
    {
        lock (_lock)
        {
            _phase = DecoderPhases.Failed;
            _reason = reason;
        }
    }

    private void SetReceived(long received)
    {
        lock (_lock)
        {
            _received = received;
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DecoderLog.NotRemoved(_logger, Path.GetFileName(path), exception.GetType().Name);
        }
    }
}
