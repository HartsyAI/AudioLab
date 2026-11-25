using System.Text;
using AudioLab.Server.Media;

namespace AudioLab.Server.Decoding;

/// <summary>
/// Turns one compressed file into a 16-bit stereo WAV file at 48 kHz by running the pinned ffmpeg. The arguments are a fixed list with
/// the two temporary paths, the input may be read from files only, and the run ends at the timeout, at the output limit or at exit.
/// </summary>
public sealed class FfmpegDecoder(DecoderOptions options, IDecoderLauncher launcher, DecoderLocation location, ILogger<FfmpegDecoder> logger)
{
    /// <summary>The rate of the WAV file a decode writes, the project rate.</summary>
    public const int OutputSampleRate = 48000;

    private const int OutputChannels = 2;
    private const string OutputCodec = "pcm_s16le";
    private const int StderrLogChars = 512;
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan _reapWait = TimeSpan.FromSeconds(10);

    /// <summary>Decodes <paramref name="input"/> to <paramref name="output"/>, which must not exist yet, and removes the output when the decode fails.</summary>
    /// <exception cref="MediaRejectedException">The file is not audio the decoder reads, or the decode took longer than the time limit.</exception>
    /// <exception cref="MediaTooLargeException">The decoded audio grew past <paramref name="maxOutputBytes"/>.</exception>
    public async Task DecodeAsync(string binary, string input, string output, long maxOutputBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(binary);
        ArgumentException.ThrowIfNullOrEmpty(input);
        ArgumentException.ThrowIfNullOrEmpty(output);
        // The caller removes the output on any outcome, including a cancelled request; this method removes it when the decode itself fails.
        IDecoderProcess process = launcher.Start(binary, Arguments(input, output), location.TempFolder);
        using IDecoderProcess owned = process;
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(options.Timeout);
        bool timedOut = false;
        bool tooLarge = false;
        Task<string> stderr = CaptureAsync(process.StandardError, options.StderrCapBytes);
        Task drain = process.StandardOutput.CopyToAsync(Stream.Null, CancellationToken.None);
        try
        {
            while (!process.HasExited)
            {
                if (FileLength(output) > maxOutputBytes)
                {
                    tooLarge = true;
                    break;
                }
                await Task.Delay(_pollInterval, limit.Token);
            }
            if (!tooLarge)
            {
                await process.WaitForExitAsync(limit.Token);
            }
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
        }
        finally
        {
            if (!process.HasExited)
            {
                await StopAsync(process);
            }
        }
        string message = (await stderr).Trim();
        await drain;
        DecodeOutcome(process.HasExited ? process.ExitCode : -1, output, message, timedOut, tooLarge, maxOutputBytes);
    }

    private void DecodeOutcome(int exitCode, string output, string stderr, bool timedOut, bool tooLarge, long maxOutputBytes)
    {
        if (timedOut)
        {
            DecoderLog.DecodeStopped(logger, "the time limit");
            TryDelete(output);
            throw new MediaRejectedException("The file took longer to decode than the server allows, so it was stopped.");
        }
        if (tooLarge)
        {
            DecoderLog.DecodeStopped(logger, "the output limit");
            TryDelete(output);
            throw new MediaTooLargeException(maxOutputBytes);
        }
        if (exitCode != 0 || FileLength(output) == 0)
        {
            DecoderLog.DecodeEnded(logger, exitCode, Truncate(stderr));
            TryDelete(output);
            throw new MediaRejectedException("The file is not audio the server can read.");
        }
    }

    private static IReadOnlyList<string> Arguments(string input, string output) =>
    [
        "-nostdin",
        "-hide_banner",
        "-loglevel",
        "error",
        "-protocol_whitelist",
        "file",
        "-y",
        "-i",
        input,
        "-map",
        "0:a:0",
        "-vn",
        "-sn",
        "-dn",
        "-map_metadata",
        "-1",
        "-ac",
        OutputChannels.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "-ar",
        OutputSampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "-c:a",
        OutputCodec,
        "-f",
        "wav",
        output,
    ];

    // Keeps the first cap bytes for the log and reads the rest, so the process never blocks on a full pipe.
    private static async Task<string> CaptureAsync(Stream stream, int cap)
    {
        byte[] buffer = new byte[4096];
        using MemoryStream kept = new();
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            int room = cap - (int)kept.Length;
            if (room > 0)
            {
                kept.Write(buffer, 0, Math.Min(room, read));
            }
        }
        return Encoding.UTF8.GetString(kept.ToArray());
    }

    // Kills the tree and waits briefly for the exit, so the output file is no longer held when the caller removes it.
    private async Task StopAsync(IDecoderProcess process)
    {
        if (!process.KillTree())
        {
            DecoderLog.DecodeStopped(logger, "the process could not be killed");
            return;
        }
        using CancellationTokenSource wait = new(_reapWait);
        try
        {
            await process.WaitForExitAsync(wait.Token);
        }
        catch (OperationCanceledException) when (wait.IsCancellationRequested)
        {
            DecoderLog.DecodeStopped(logger, "the process did not exit after it was killed");
        }
    }

    private static string Truncate(string text) => text.Length <= StderrLogChars ? text : text[..StderrLogChars];

    private static long FileLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DecoderLog.NotRemoved(logger, Path.GetFileName(path), exception.GetType().Name);
        }
    }
}
