using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Renders a project straight into a WAV file, a block at a time, so a long export never holds the whole render in memory.</summary>
public static class WavExporter
{
    private const int BlockFrames = 4096;

    /// <summary>Writes the mix, or the stem named in the render options, to a stream; returns what was found in the samples.</summary>
    public static WavResult Write(ProjectDocument document, IMediaSource media, Stream output, WavOptions wav, OfflineOptions? render = null)
    {
        ArgumentNullException.ThrowIfNull(wav);
        OfflineOptions settings = OfflineRenderer.Validate(render);
        RenderPlan plan = RenderPlan.Create(document, media, OfflineRenderer.PlanOptionsOf(settings));
        return Write(plan, output, wav, settings);
    }

    /// <summary>Writes a prepared plan over the range the options name.</summary>
    internal static WavResult Write(RenderPlan plan, Stream output, WavOptions wav, OfflineOptions settings)
    {
        (long start, long frames) = OfflineRenderer.RangeOf(plan, settings);
        WavStreamWriter writer = new(output, plan.Format.SampleRate, frames, wav);
        MixerGraph graph = new(plan, settings.BlockFrames);
        graph.Seek(start);
        float[] left = new float[BlockFrames];
        float[] right = new float[BlockFrames];
        for (long done = 0; done < frames; done += BlockFrames)
        {
            int count = (int)Math.Min(BlockFrames, frames - done);
            graph.Render(left.AsSpan(0, count), right.AsSpan(0, count));
            writer.Write(left.AsSpan(0, count), right.AsSpan(0, count));
        }
        writer.Complete();
        return writer.Result;
    }

    /// <summary>Writes audio that is already rendered.</summary>
    public static WavResult Write(RenderedAudio audio, Stream output, WavOptions wav)
    {
        ArgumentNullException.ThrowIfNull(audio);
        WavStreamWriter writer = new(output, audio.Format.SampleRate, audio.Frames, wav);
        writer.Write(audio.Left, audio.Right);
        writer.Complete();
        return writer.Result;
    }
}
