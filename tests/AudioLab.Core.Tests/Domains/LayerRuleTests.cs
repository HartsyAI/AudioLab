using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class LayerRuleTests
{
    private static AudioClip Clip(string id, double start, double length, double fadeIn = 0, double fadeOut = 0, bool muted = false) => new()
    {
        Id = ClipId.From(id),
        Name = id,
        Media = MediaId.From("m"),
        Start = start,
        Duration = length,
        Offset = 0,
        TrimEnd = 0,
        Gain = 1,
        FadeIn = fadeIn,
        FadeOut = fadeOut,
        Muted = muted,
    };

    [Fact]
    public void ClipsThatDoNotOverlapAreHeardWhole()
    {
        IReadOnlyList<AudibleSpan> spans = LayerRules.AudibleSpans([Clip("a", 0, 1), Clip("b", 2, 1)]);

        Assert.Equal([new AudibleSpan(ClipId.From("a"), 0, 1, false, false), new AudibleSpan(ClipId.From("b"), 2, 3, false, false)], spans);
    }

    [Fact]
    public void ALaterClipCoversTheOnesBeforeAndLeavesSeamsOnBothSides()
    {
        IReadOnlyList<AudibleSpan> spans = LayerRules.AudibleSpans([Clip("a", 0, 4), Clip("b", 1, 1)]);

        Assert.Equal(
            [
                new AudibleSpan(ClipId.From("a"), 0, 1, false, true),
                new AudibleSpan(ClipId.From("a"), 2, 4, true, false),
                new AudibleSpan(ClipId.From("b"), 1, 2, false, false),
            ],
            spans);
    }

    [Fact]
    public void AClipCoveredWholeIsNotHeardAndAMutedClipNeitherPlaysNorCovers()
    {
        IReadOnlyList<AudibleSpan> hidden = LayerRules.AudibleSpans([Clip("a", 1, 1), Clip("b", 0, 3)]);
        IReadOnlyList<AudibleSpan> letThrough = LayerRules.AudibleSpans([Clip("a", 0, 2), Clip("b", 0, 2, muted: true)]);

        Assert.Equal([new AudibleSpan(ClipId.From("b"), 0, 3, false, false)], hidden);
        Assert.Equal([new AudibleSpan(ClipId.From("a"), 0, 2, false, false)], letThrough);
    }

    [Fact]
    public void ACrossfadeKeepsBothClipsPlayingOverTheOverlap()
    {
        // a fades out over 1 s into the fade-in of b: both are heard where they overlap.
        IReadOnlyList<AudibleSpan> spans = LayerRules.AudibleSpans([Clip("a", 0, 3, fadeOut: 1), Clip("b", 2, 3, fadeIn: 1)]);

        Assert.Equal(
            [new AudibleSpan(ClipId.From("a"), 0, 3, false, false), new AudibleSpan(ClipId.From("b"), 2, 5, false, false)],
            spans);
    }
}
