using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class SectionRuleTests
{
    private static readonly SectionId _a = SectionId.From("sec_a");

    private static readonly SectionId _b = SectionId.From("sec_b");

    private static readonly ValueList<Section> _two = [Make(_a, 0, 10, "A"), Make(_b, 20, 30, "B")];

    [Fact]
    public void ASectionHoldsItsStartButNotItsEnd()
    {
        Assert.Equal(_a, SectionRules.At(_two, 0)!.Id);
        Assert.Null(SectionRules.At(_two, 10));
        Assert.Equal(_b, SectionRules.At(_two, 20)!.Id);
    }

    [Fact]
    public void NeighboursAreFoundByTheirStart()
    {
        Assert.Equal(_b, SectionRules.Neighbor(_two, 5, 1)!.Id);
        Assert.Equal(_b, SectionRules.Neighbor(_two, 25, -1)!.Id);
        Assert.Equal(_a, SectionRules.Neighbor(_two, 15, -1)!.Id);
        Assert.Null(SectionRules.Neighbor(_two, 0, -1));
        Assert.Null(SectionRules.Neighbor(_two, 25, 1));
    }

    [Theory]
    [InlineData(15, 10, 20)]
    [InlineData(-5, 0, 10)]
    [InlineData(3, 3, 13)]
    public void MovingASectionStopsAtItsNeighbours(double start, double expectedStart, double expectedEnd)
    {
        SectionSpan span = SectionRules.Constrain(_two, _a, start, 0, SectionEdit.Move);

        Assert.Equal(new SectionSpan(expectedStart, expectedEnd), span);
    }

    [Fact]
    public void MovingTheLaterSectionBackStopsAtTheEarlierOne()
    {
        Assert.Equal(new SectionSpan(10, 20), SectionRules.Constrain(_two, _b, 5, 0, SectionEdit.Move));
    }

    [Fact]
    public void AnEdgeStopsAtTheNeighbourAndAtTheMinimumLength()
    {
        Assert.Equal(new SectionSpan(10, 30), SectionRules.Constrain(_two, _b, 5, 0, SectionEdit.Start));
        Assert.Equal(new SectionSpan(29.9, 30), SectionRules.Constrain(_two, _b, 99, 0, SectionEdit.Start), new SpanComparer());
        Assert.Equal(new SectionSpan(0, 20), SectionRules.Constrain(_two, _a, 0, 99, SectionEdit.End));
        Assert.Equal(new SectionSpan(0, 0.1), SectionRules.Constrain(_two, _a, 0, 0, SectionEdit.End));
    }

    [Fact]
    public void AnUnknownSectionKeepsTheRequestedSpan()
    {
        Assert.Equal(new SectionSpan(1, 2), SectionRules.Constrain(_two, SectionId.From("sec_none"), 1, 2, SectionEdit.Move));
    }

    [Theory]
    [InlineData(12, 5, 12, 17)]
    [InlineData(12, 50, 12, 20)]
    [InlineData(10, 5, 10, 15)]
    [InlineData(35, 4, 35, 39)]
    public void ANewSectionFitsInTheGapAndIsCutShortAtTheNext(double start, double length, double expectedStart, double expectedEnd)
    {
        Assert.Equal(new SectionSpan(expectedStart, expectedEnd), SectionRules.Fit(_two, start, length));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void NoSectionFitsInsideAnotherOrBeforeZero(double start)
    {
        Assert.Null(SectionRules.Fit(_two, start, 4));
    }

    [Fact]
    public void NoSectionFitsInAGapShorterThanTheMinimum()
    {
        ValueList<Section> tight = [Make(_a, 0, 10, "A"), Make(_b, 10.05, 20, "B")];

        Assert.Null(SectionRules.Fit(tight, 10, 5));
    }

    [Fact]
    public void ADefaultNameIsNumberedAndUnused()
    {
        Assert.Equal("Section 1", SectionRules.UniqueName([]));
        Assert.Equal("Section 3", SectionRules.UniqueName([Make(_a, 0, 1, "Section 2")]));
        Assert.Equal("Section 3", SectionRules.UniqueName(_two));
    }

    [Fact]
    public void SectionsFromMarkersRunToTheNextMarkerAndTheLastToTheEnd()
    {
        Marker[] markers =
        [
            Mark("mrk_1", 20, "Verse", "#ffffff"),
            Mark("mrk_2", 0, "  ", null),
            Mark("mrk_3", 5, "Hook", null),
        ];

        IReadOnlyList<SectionDraft> drafts = SectionRules.FromMarkers(markers, 30);

        Assert.Equal(
            [
                new SectionDraft(0, 5, "Section 1", ProjectDefaults.SectionColor(0)),
                new SectionDraft(5, 20, "Hook", ProjectDefaults.SectionColor(1)),
                new SectionDraft(20, 30, "Verse", "#ffffff"),
            ],
            drafts);
    }

    [Fact]
    public void MarkersTooCloseOrPastTheEndMakeNoSection()
    {
        Marker[] markers = [Mark("mrk_1", 0, "Close", null), Mark("mrk_2", 0.05, "Kept", null), Mark("mrk_3", 40, "Late", null)];

        IReadOnlyList<SectionDraft> drafts = SectionRules.FromMarkers(markers, 30);

        SectionDraft only = Assert.Single(drafts);
        Assert.Equal("Kept", only.Name);
        Assert.Equal(0.05, only.Start);
        Assert.Equal(40, only.End);
    }

    private static Section Make(SectionId id, double start, double end, string name)
    {
        return new Section { Id = id, Start = start, End = end, Name = name, Color = "#f4a261" };
    }

    private static Marker Mark(string id, double time, string name, string? color)
    {
        return new Marker { Id = MarkerId.From(id), Time = time, Name = name, Color = color };
    }

    private sealed class SpanComparer : IEqualityComparer<SectionSpan>
    {
        public bool Equals(SectionSpan x, SectionSpan y) => Math.Abs(x.Start - y.Start) < 1e-9 && Math.Abs(x.End - y.End) < 1e-9;

        public int GetHashCode(SectionSpan obj) => obj.GetHashCode();
    }
}
