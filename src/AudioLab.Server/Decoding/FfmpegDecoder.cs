using System.Text;
using AudioLab.Server.Media;

namespace AudioLab.Server.Decoding;

/// <summary>Turns one compressed file into a 16-bit stereo WAV file at 48 kHz by running the pinned ffmpeg. The arguments are a
/// fixed list with the two temporary paths, the input may be read from files only, and the run ends at the timeout, at
/// the output limit or at exit.</summary>
public sealed class FfmpegDecoder(DecoderOptions options, IDecoderLauncher launcher, DecoderLocation location, ILogger<FfmpegDecoder> logger)
{
    /// <summary>The rate of the WAV file a decode writes, the project rate.</summary>
    public const int OutputSampleRate = 48000;

    private const int OutputChannels = 2;
    private const string OutputCodec = "pcm_s16le";
    // The log gets a short excerpt of the error output, with control characters replaced, so a file cannot write lines into the log.
    private const int StderrLogChars = 200;
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan _reapWait = TimeSpan.FromSeconds(10);

    /// <summary>Decodes <paramref name="input"/> to <paramref name="output"/>, which must not exist yet, and removes the output
    /// when the decode fails.</summary>
    /// <param name="binary">The installed decoder's path, as <see cref="DecoderInstaller.VerifiedBinary"/> returned it.</param>
    /// <param name="demuxer">
    /// The one demuxer the input may be read with, chosen from its content type, so a file of another format is refused.
    /// </param>
    /// <param name="input">The compressed file, a temporary file the caller wrote.</param>
    /// <param name="output">Where the WAV is written; it must not exist yet.</param>
    /// <param name="maxOutputBytes">The most WAV the decode may write before it is stopped.</param>
    /// <param name="cancellationToken">Ends the decode, as the caller's request ending does.</param>
    /// <exception cref="DecoderNotInstalledException">The process could not be started at all.</exception>
    /// <exception cref="MediaRejectedException">The file is not audio the decoder reads, or the decode took longer than the time limit.</exception>
    /// <exception cref="MediaTooLargeException">The decoded audio grew past <paramref name="maxOutputBytes"/>.</exception>
    public async Task DecodeAsync(
        string binary,
        string demuxer,
        string input,
        string output,
        long maxOutputBytes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(binary);
        ArgumentException.ThrowIfNullOrEmpty(demuxer);
        ArgumentException.ThrowIfNullOrEmpty(input);
        ArgumentException.ThrowIfNullOrEmpty(output);
        // The caller removes the output on any outcome, including a cancelled request; this method removes it when the decode itself fails.
        IDecoderProcess process = StartProcess(binary, Arguments(demuxer, input, output));
        using IDecoderProcess owned = process;
        using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(options.Timeout);
        bool timedOut = false;
        bool tooLarge = false;
        Task<StderrCapture> stderr = CaptureAsync(process.StandardError, options.StderrCapBytes);
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
        // After the kill, a pipe that a surviving grandchild holds must not keep the decode waiting, so both reads are bounded.
        StderrCapture captured = await WithinAsync(stderr, new StderrCapture(string.Empty, 0, 0));
        await WithinAsync(drain);
        // The output is measured after the exit too: a process that finished quickly can still have written past the limit.
        tooLarge |= FileLength(output) > maxOutputBytes;
        DecodeOutcome(process.HasExited ? process.ExitCode : -1, output, captured, timedOut, tooLarge, maxOutputBytes);
    }

    private IDecoderProcess StartProcess(string binary, IReadOnlyList<string> arguments)
    {
        try
        {
            return launcher.Start(binary, arguments, location.TempFolder);
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            DecoderLog.Refused(logger, $"the process could not be started ({exception.NativeErrorCode})");
            throw new DecoderNotInstalledException("The audio decoder could not be started on this server, so the file was not decoded.");
        }
    }

    // Waits for the task for at most the reap wait; a task that is still running then is given up on, and the fallback is returned.
    private static async Task<T> WithinAsync<T>(Task<T> task, T fallback)
    {
        Task finished = await Task.WhenAny(task, Task.Delay(_reapWait));
        return finished == task ? await task : fallback;
    }

    private static async Task WithinAsync(Task task)
    {
        await Task.WhenAny(task, Task.Delay(_reapWait));
    }

    private void DecodeOutcome(int exitCode, string output, StderrCapture captured, bool timedOut, bool tooLarge, long maxOutputBytes)
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
            DecoderLog.DecodeEnded(logger, exitCode, Truncate(captured.Text.Trim()), captured.Kept, captured.Total);
            TryDelete(output);
            throw new MediaRejectedException("The file is not audio the server can read.");
        }
    }

    private static IReadOnlyList<string> Arguments(string demuxer, string input, string output) =>
    [
        "-nostdin",
        "-hide_banner",
        "-loglevel",
        "error",
        "-protocol_whitelist",
        "file",
        "-format_whitelist",
        demuxer,
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
    private static async Task<StderrCapture> CaptureAsync(Stream stream, int cap)
    {
        byte[] buffer = new byte[4096];
        using MemoryStream kept = new();
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            total += read;
            int room = cap - (int)kept.Length;
            if (room > 0)
            {
                kept.Write(buffer, 0, Math.Min(room, read));
            }
        }
        return new StderrCapture(Encoding.UTF8.GetString(kept.ToArray()), kept.Length, total);
    }

    /// <summary>What the decoder wrote to its error output: the kept text, how many bytes were kept, and how many it wrote in all.</summary>
    private sealed record StderrCapture(string Text, long Kept, long Total);

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

    // Control characters become spaces, so the excerpt is one line of plain text whatever the decoder wrote.
    private static string Truncate(string text)
    {
        string plain = new(text.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
        return plain.Length <= StderrLogChars ? plain : plain[..StderrLogChars];
    }

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
