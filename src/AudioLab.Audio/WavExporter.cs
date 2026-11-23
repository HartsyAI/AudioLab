using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Renders a project straight into a WAV file, a block at a time, so a long export never holds the whole render in memory.</summary>
/// <remarks>
/// The synchronous members are for streams that take synchronous writes, such as a file or memory. A server writes to a response body
/// with <see cref="WriteAsync(ProjectDocument, IMediaSource, Stream, WavOptions, OfflineOptions?, CancellationToken)"/> and the request's
/// cancellation token. Rendering runs on the calling thread between the awaits, so a server that must not block a request thread
/// starts the call on a worker. A render that fails or is cancelled leaves an incomplete file, which the caller discards.
/// </remarks>
public static class WavExporter
{
    private const int BlockFrames = 4096;

    /// <summary>The size of the file that writing the mix, or the stem named in the render options, would make, with the same lengths.</summary>
    /// <remarks>Only the lengths are planned, so no effect is built; the media is asked for, and a missing file fails as a write would.</remarks>
    /// <exception cref="ExportTooLargeException">The file would be more than a WAV file can hold, 4 GiB.</exception>
    public static ExportSize Measure(ProjectDocument document, IMediaSource media, WavOptions wav, OfflineOptions? render = null)
    {
        ArgumentNullException.ThrowIfNull(wav);
        OfflineOptions settings = OfflineRenderer.Validate(render);
        RenderPlan plan = RenderPlan.Create(document, media, OfflineRenderer.PlanOptionsOf(settings) with { SizingOnly = true });
        (_, long frames) = OfflineRenderer.RangeOf(plan, settings);
        try
        {
            return new ExportSize(WavStreamWriter.SizeOf(frames, wav), frames, 1);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new ExportTooLargeException(exception.Message);
        }
    }

    /// <summary>Writes the mix, or the stem named in the render options, to a stream; returns what was found in the samples.</summary>
    public static WavResult Write(ProjectDocument document, IMediaSource media, Stream output, WavOptions wav, OfflineOptions? render = null)
    {
        ArgumentNullException.ThrowIfNull(wav);
        OfflineOptions settings = OfflineRenderer.Validate(render);
        RenderPlan plan = RenderPlan.Create(document, media, OfflineRenderer.PlanOptionsOf(settings));
        return Write(plan, output, wav, settings);
    }

    /// <summary>Writes the mix, or a stem, to a stream that takes asynchronous writes, and stops when the token is cancelled.</summary>
    public static Task<WavResult> WriteAsync(
        ProjectDocument document,
        IMediaSource media,
        Stream output,
        WavOptions wav,
        OfflineOptions? render = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wav);
        OfflineOptions settings = OfflineRenderer.Validate(render);
        RenderPlan plan = RenderPlan.Create(document, media, OfflineRenderer.PlanOptionsOf(settings));
        return WriteAsync(plan, output, wav, settings, cancellationToken);
    }

    /// <summary>Writes audio that is already rendered.</summary>
    public static WavResult Write(RenderedAudio audio, Stream output, WavOptions wav)
    {
        ArgumentNullException.ThrowIfNull(audio);
        WavStreamWriter writer = new(output, audio.Format.SampleRate, audio.Frames, wav);
        writer.WriteHeader();
        writer.Write(audio.Left, audio.Right);
        writer.Complete();
        return writer.Result;
    }

    /// <summary>Writes a prepared plan over the range the options name.</summary>
    internal static WavResult Write(RenderPlan plan, Stream output, WavOptions wav, OfflineOptions settings)
    {
        (long start, long frames) = OfflineRenderer.RangeOf(plan, settings);
        WavStreamWriter writer = new(output, plan.Format.SampleRate, frames, wav);
        MixerGraph graph = Begin(plan, settings, start);
        writer.WriteHeader();
        float[] left = new float[BlockFrames];
        float[] right = new float[BlockFrames];
        long fade = FadeFrames(plan, settings);
        for (long done = 0; done < frames; done += BlockFrames)
        {
            int count = (int)Math.Min(BlockFrames, frames - done);
            RenderBlock(graph, left.AsSpan(0, count), right.AsSpan(0, count), done, frames, fade);
            writer.Write(left.AsSpan(0, count), right.AsSpan(0, count));
        }
        writer.Complete();
        return writer.Result;
    }

    /// <summary>Writes a prepared plan to a stream that takes asynchronous writes, a block at a time.</summary>
    internal static async Task<WavResult> WriteAsync(
        RenderPlan plan,
        Stream output,
        WavOptions wav,
        OfflineOptions settings,
        CancellationToken cancellationToken)
    {
        (long start, long frames) = OfflineRenderer.RangeOf(plan, settings);
        WavStreamWriter writer = new(output, plan.Format.SampleRate, frames, wav);
        MixerGraph graph = Begin(plan, settings, start);
        await writer.WriteHeaderAsync(cancellationToken).ConfigureAwait(false);
        float[] left = new float[BlockFrames];
        float[] right = new float[BlockFrames];
        long fade = FadeFrames(plan, settings);
        for (long done = 0; done < frames; done += BlockFrames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(BlockFrames, frames - done);
            RenderBlock(graph, left.AsSpan(0, count), right.AsSpan(0, count), done, frames, fade);
            await writer.WriteAsync(left.AsMemory(0, count), right.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
        await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        return writer.Result;
    }

    private static MixerGraph Begin(RenderPlan plan, OfflineOptions settings, long start)
    {
        MixerGraph graph = new(plan, settings.BlockFrames);
        graph.Seek(start);
        return graph;
    }

    private static long FadeFrames(RenderPlan plan, OfflineOptions settings) =>
        settings.IncludeTail && plan.TailIsCut ? TailFade.FramesAt(settings.SampleRate) : 0;

    private static void RenderBlock(MixerGraph graph, Span<float> left, Span<float> right, long done, long frames, long fade)
    {
        graph.Render(left, right);
        if (fade > 0)
        {
            TailFade.Apply(left, right, done, frames, fade);
        }
    }
}
