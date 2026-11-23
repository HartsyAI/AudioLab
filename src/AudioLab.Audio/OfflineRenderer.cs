using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Renders a project to a float buffer by pulling <see cref="MixerGraph"/> in a loop, the same graph live playback runs.</summary>
public static class OfflineRenderer
{
    /// <summary>Renders the document from <see cref="OfflineOptions.Start"/> to its end, plus the tail when asked.</summary>
    public static RenderedAudio Render(ProjectDocument document, IMediaSource media, OfflineOptions? options = null)
    {
        OfflineOptions settings = options ?? new OfflineOptions();
        if (!double.IsFinite(settings.Start) || settings.Start < 0 || (settings.End is { } end && !double.IsFinite(end)))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The start and end must be finite times of at least 0.");
        }
        if (settings.End is { } endTime && endTime < settings.Start)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The end must not be before the start.");
        }
        RenderPlan plan = RenderPlan.Create(document, media, new PlanOptions
        {
            SampleRate = settings.SampleRate,
            Sources = settings.Sources,
            Cache = settings.Cache,
        });
        long start = RenderPlan.Frames(settings.Start, settings.SampleRate);
        long finish = settings.End is { } given ? RenderPlan.Frames(given, settings.SampleRate) : plan.ContentFrames;
        long total = Math.Max(0, finish - start) + (settings.IncludeTail ? plan.TailFrames : 0);
        if (total > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The render would be longer than a buffer can hold.");
        }
        float[] left = new float[total];
        float[] right = new float[total];
        MixerGraph graph = new(plan, settings.BlockFrames);
        graph.Seek(start);
        graph.Render(left, right);
        return new RenderedAudio(plan.Format, left, right);
    }
}
