using AudioLab.Core.Model;
using AudioLab.Server.Media;
using AudioLab.Server.Services;

namespace AudioLab.Server.Decoding;

/// <summary>Stores a compressed upload: spools it to a temporary file under the limit, decodes it to WAV, then hands the WAV to
/// the media service, which checks it with the strict WAV reader and stores it like any upload. Every temporary file is
/// removed on any outcome.</summary>
public sealed class DecodedUploadService(
    MediaService media,
    DecoderInstaller installer,
    FfmpegDecoder decoder,
    DecoderLocation location,
    DecoderOptions options,
    MediaOptions mediaOptions,
    [FromKeyedServices(DecodedUploadService.GateKey)] ConcurrencyGate gate,
    ILogger<DecodedUploadService> logger)
{
    /// <summary>The key of the gate that counts decodes, per user and in all, with the limits of <c>AudioLab:Decoder</c>.</summary>
    public const string GateKey = "decode";

    private const int BufferBytes = 81920;

    /// <summary>Stores a compressed file as WAV.</summary>
    /// <exception cref="DecoderNotInstalledException">The decoder is not installed, or its binary does not match its checksum.</exception>
    /// <exception cref="MediaRejectedException">The name is not valid, the file is empty, or it is not audio the decoder reads.</exception>
    /// <exception cref="MediaTooLargeException">The file is larger than <c>MaxInputBytes</c>, or its decoded audio is larger than the upload limit.</exception>
    /// <exception cref="GateBusyException">The user or the server is already running as many decodes as are allowed.</exception>
    public async Task<MediaRecord> UploadAsync(UserId user, string? name, Stream body, long? declaredLength, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        IReadOnlyList<string> nameProblems = MediaValidator.ValidateName(name);
        if (nameProblems.Count > 0)
        {
            throw new MediaRejectedException($"The name is not valid: {nameProblems[0]}");
        }
        string binary = installer.VerifiedBinary()
            ?? throw new DecoderNotInstalledException(
                "Compressed audio needs the audio decoder, which is not installed. Install it with Install audio decoder on the Media tab, then upload again.");
        if (declaredLength > options.MaxInputBytes)
        {
            throw new MediaTooLargeException(options.MaxInputBytes);
        }
        string id = Guid.NewGuid().ToString("N");
        string source = Path.Combine(location.TempFolder, $"{id}.src");
        string output = Path.Combine(location.TempFolder, $"{id}.wav");
        try
        {
            await SpoolAsync(body, source, cancellationToken);
            using (gate.Enter(user))
            {
                await decoder.DecodeAsync(binary, source, output, mediaOptions.MaxUploadBytes, cancellationToken);
            }
            await using FileStream decoded = new(output, FileMode.Open, FileAccess.Read, FileShare.Read, BufferBytes, FileOptions.Asynchronous);
            return await media.UploadAsync(user, name, decoded, decoded.Length, cancellationToken);
        }
        finally
        {
            TryDelete(source);
            TryDelete(output);
        }
    }

    // Counted on the bytes that arrive, as the media upload counts them, so a client that announces less than it sends is still held.
    private async Task SpoolAsync(Stream body, string path, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferBytes];
        long total = 0;
        await using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferBytes, FileOptions.Asynchronous);
        int read;
        while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > options.MaxInputBytes)
            {
                throw new MediaTooLargeException(options.MaxInputBytes);
            }
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (total == 0)
        {
            throw new MediaRejectedException("The upload is empty.");
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
            DecoderLog.NotRemoved(logger, Path.GetFileName(path), exception.GetType().Name);
        }
    }
}
