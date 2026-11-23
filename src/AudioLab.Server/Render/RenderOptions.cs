namespace AudioLab.Server.Render;

/// <summary>What a render of a project may use, under <c>AudioLab:Render</c>.</summary>
public sealed record RenderOptions
{
    /// <summary>The most bytes one media file may take as float samples, as it is and at the render's rate; 1 GiB by default.</summary>
    /// <remarks>
    /// Only the first two channels are kept, as the engine uses no more. Both sizes are worked out from the header before anything is
    /// allocated, so a file that would pass at its own rate and not at the render's is refused as soon as the rate is known. The file
    /// is held in both forms while it is converted, so one file takes at most twice this.
    /// </remarks>
    public long MaxDecodedBytesPerFile { get; init; } = 1024L * 1024 * 1024;

    /// <summary>The most bytes of media one render holds at the render's rate, every distinct file counted once; 2 GiB by default.</summary>
    /// <remarks>A render that needs more is refused with a limit: it never decodes a file twice and never evicts one.</remarks>
    public long MaxDecodedBytesPerRender { get; init; } = 2L * 1024 * 1024 * 1024;
}
