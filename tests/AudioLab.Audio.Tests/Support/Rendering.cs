using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests.Support;

/// <summary>Shortcuts for rendering the small projects of the tests.</summary>
public static class Rendering
{
    /// <summary>A low rate keeps the tests fast; the engine does not care.</summary>
    public const int Rate = 8000;

    public static RenderedAudio Render(ProjectDocument document, MemoryMedia media, int rate = Rate, double? end = null) =>
        OfflineRenderer.Render(document, media, new OfflineOptions { SampleRate = rate, End = end, IncludeTail = false });

    /// <summary>The value both channels hold at <paramref name="seconds"/>, which must be the same on both.</summary>
    public static float At(RenderedAudio audio, double seconds, int rate = Rate)
    {
        int frame = (int)Math.Round(seconds * rate);
        Assert.Equal(audio.Left[frame], audio.Right[frame], 6);
        return audio.Left[frame];
    }
}
