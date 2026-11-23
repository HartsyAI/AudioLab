namespace AudioLab.Server.Render;

/// <summary>What a render of a project may use, under <c>AudioLab:Render</c>.</summary>
public sealed record RenderOptions
{
    /// <summary>The most bytes of decoded float samples one media file may take while a render reads it; the default is 1 GiB.</summary>
    /// <remarks>A 16-bit upload at the default upload limit decodes to twice its size, so this is the most an upload may need.</remarks>
    public long MaxDecodedBytesPerFile { get; init; } = 1024L * 1024 * 1024;

    /// <summary>The most bytes of cleaned, resampled media one render keeps at once; the file in use is always kept. The default is 512 MiB.</summary>
    public long MediaCacheBytes { get; init; } = 512L * 1024 * 1024;
}
