using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Renders a project to a float buffer by pulling <see cref="MixerGraph"/> in a loop, the same graph live playback runs.</summary>
public static class OfflineRenderer
{
    /// <summary>Renders the document from <see cref="OfflineOptions.Start"/> to its end, plus the tail when asked.</summary>
    public static RenderedAudio Render(ProjectDocument document, IMediaSource media, OfflineOptions? options = null)
    {
        OfflineOptions settings = Validate(options);
        RenderPlan plan = RenderPlan.Create(document, media, PlanOptionsOf(settings));
        return Render(plan, settings);
    }

    internal static PlanOptions PlanOptionsOf(OfflineOptions settings) =>
        new() { SampleRate = settings.SampleRate, Sources = settings.Sources, StemOf = settings.StemOf };

    internal static OfflineOptions Validate(OfflineOptions? options)
    {
        OfflineOptions settings = options ?? new OfflineOptions();
        if (!double.IsFinite(settings.Start) || settings.Start < 0 || (settings.End is { } end && !double.IsFinite(end)))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The start and end must be finite times of at least 0.");
        }
        return settings;
    }

    /// <summary>Renders a prepared plan over the range the options name.</summary>
    internal static RenderedAudio Render(RenderPlan plan, OfflineOptions settings)
    {
        (long start, long total) = RangeOf(plan, settings);
        if (total > Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "The render would be longer than a buffer can hold.");
        }
        float[] left = new float[total];
        float[] right = new float[total];
        MixerGraph graph = new(plan, settings.BlockFrames);
        graph.Seek(start);
        graph.Render(left, right);
        return new RenderedAudio(plan.Format, left, right);
    }

    /// <summary>The first frame of a render and how many frames it runs for.</summary>
    internal static (long Start, long Frames) RangeOf(RenderPlan plan, OfflineOptions settings)
    {
        long start = RenderPlan.Frames(settings.Start, settings.SampleRate);
        long finish = settings.End is { } given ? RenderPlan.Frames(given, settings.SampleRate) : plan.ContentFrames;
        long tail = settings.IncludeTail ? settings.TailFrames ?? plan.TailFrames : 0;
        return (start, Math.Max(0, finish - start) + tail);
    }
}
