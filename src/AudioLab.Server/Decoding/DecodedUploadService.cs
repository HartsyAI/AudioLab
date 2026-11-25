using AudioLab.Core.Model;
using AudioLab.Server.Media;
using AudioLab.Server.Services;

namespace AudioLab.Server.Decoding;

/// <summary>Stores a compressed upload. The decode slot is taken before anything is spooled, so a refused upload writes nothing.
/// The file is spooled under the input limit, decoded to WAV, and the WAV is handed to the media service, which checks
/// it with the strict WAV reader and stores it like any upload. Every temporary file is removed on any outcome.</summary>
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

    /// <summary>The demuxer the decoder may read a file of this content type with, so a file that is not the type it says it is is refused.</summary>
    /// <returns>The demuxer's name, or null when the content type is not a compressed type this server decodes.</returns>
    public static string? DemuxerFor(string mediaType) => mediaType.ToLowerInvariant() switch
    {
        "audio/mpeg" or "audio/mp3" => "mp3",
        "audio/mp4" or "audio/m4a" or "audio/x-m4a" => "mov",
        "audio/flac" or "audio/x-flac" => "flac",
        "audio/ogg" or "application/ogg" => "ogg",
        _ => null,
    };

    /// <summary>Stores a compressed file as WAV.</summary>
    /// <exception cref="DecoderNotInstalledException">
    /// The decoder is not installed, is not available on this system, or its binary does not match its checksum.
    /// </exception>
    /// <exception cref="MediaRejectedException">The name is not valid, the file is empty, or it is not audio the decoder reads.</exception>
    /// <exception cref="MediaTooLargeException">
    /// The file is larger than <c>MaxInputBytes</c>, or its decoded audio is larger than the upload limit.
    /// </exception>
    /// <exception cref="MediaQuotaExceededException">The user has no room left for a decoded file of the largest size the upload allows.</exception>
    /// <exception cref="GateBusyException">The user or the server is already running as many decodes as are allowed.</exception>
    public async Task<MediaRecord> UploadAsync(
        UserId user,
        string mediaType,
        string? name,
        Stream body,
        long? declaredLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        string demuxer = DemuxerFor(mediaType) ?? throw new MediaRejectedException("That content type is not a compressed audio type.");
        IReadOnlyList<string> nameProblems = MediaValidator.ValidateName(name);
        if (nameProblems.Count > 0)
        {
            throw new MediaRejectedException($"The name is not valid: {nameProblems[0]}");
        }
        if (!installer.Supported)
        {
            throw new DecoderNotInstalledException(
                "The audio decoder is not available on this system, so compressed audio cannot be uploaded here. Upload a WAV file instead.");
        }
        string binary = installer.VerifiedBinary() ?? throw NotInstalled();
        if (declaredLength > options.MaxInputBytes)
        {
            throw new MediaTooLargeException(options.MaxInputBytes);
        }
        await CheckRoomAsync(user, cancellationToken);
        string id = Guid.NewGuid().ToString("N");
        string source = Path.Combine(location.TempFolder, $"{id}.src");
        string output = Path.Combine(location.TempFolder, $"{id}.wav");
        try
        {
            // The slot is taken first: a request that finds the decodes full is refused before a byte of it is written to disk.
            using (gate.Enter(user))
            {
                await SpoolAsync(body, source, cancellationToken);
                await decoder.DecodeAsync(binary, demuxer, source, output, mediaOptions.MaxUploadBytes, cancellationToken);
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

    // The refusal names the way out: the install button when this server offers one, and a WAV upload when it does not.
    private DecoderNotInstalledException NotInstalled() => options.InstallEnabled
        ? new DecoderNotInstalledException(
            "Compressed audio needs the audio decoder, which is not installed. "
            + "Install it with Install audio decoder on the Media tab, then upload again.")
        : new DecoderNotInstalledException(
            "Compressed audio needs the audio decoder, which is not installed on this server and cannot be installed here. "
            + "Upload a WAV file instead.");

    // A decoded file can be as large as the upload limit, so room for that much is checked before the decode starts. The media
    // service still checks the real size when the file is stored, so this only refuses early; it reserves nothing.
    private async Task CheckRoomAsync(UserId user, CancellationToken cancellationToken)
    {
        MediaLimits limits = await media.LimitsAsync(user, cancellationToken);
        if (limits.FilesRemaining < 1 || limits.BytesRemaining < mediaOptions.MaxUploadBytes)
        {
            throw new MediaQuotaExceededException("There is not room left for a decoded file of the largest size an upload may be.");
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
