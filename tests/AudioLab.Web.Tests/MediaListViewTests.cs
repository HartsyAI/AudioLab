using System.Collections;
using AudioLab.Core.Model;
using AudioLab.Web.Media;

namespace AudioLab.Web.Tests;

/// <summary>Holds the tests of how a long library list is drawn: a page at a time, with its tab stop found once.</summary>
public sealed class MediaListViewTests
{
    private static MediaSummary File(int index) =>
        new(
            $"m{index}", $"f{index}.wav", MediaKind.Audio, MediaFormat.Wav, SampleEncoding.Pcm16, 1, 44100, 2, 1,
            new string('0', 64), DateTimeOffset.UnixEpoch);

    private sealed class CountingList(int count) : IReadOnlyList<MediaSummary>
    {
        public long Reads { get; private set; }

        public int Count => count;

        public MediaSummary this[int index]
        {
            get
            {
                Reads++;
                return File(index);
            }
        }

        public IEnumerator<MediaSummary> GetEnumerator() => throw new NotSupportedException("The view must index, never enumerate the whole list.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>A list of 5000 files draws one page, and building the view reads each file at most about once.</summary>
    [Fact]
    public void AListOfFiveThousandFilesDrawsOnePageAndIsReadAboutOnce()
    {
        CountingList files = new(5000);

        MediaListView view = new(files, "m4999", 0);

        Assert.Equal(MediaListView.PageSize, view.Visible.Count);
        Assert.Equal(4900, view.Hidden);
        Assert.Equal("m0", view.TabStopId);
        Assert.InRange(files.Reads, 1, 5000 + MediaListView.PageSize + 1);
    }

    /// <summary>The tab stop is the selected file when it is drawn, else the first.</summary>
    [Fact]
    public void TheTabStopIsTheSelectedFileWhenItIsDrawnElseTheFirst()
    {
        List<MediaSummary> files = [.. Enumerable.Range(0, 300).Select(File)];

        Assert.Equal("m5", new MediaListView(files, "m5", 100).TabStopId);
        Assert.Equal("m0", new MediaListView(files, "m250", 100).TabStopId);
        Assert.Equal("m250", new MediaListView(files, "m250", 300).TabStopId);
        Assert.Equal("m0", new MediaListView(files, "gone", 100).TabStopId);
        Assert.Null(new MediaListView([], null, 100).TabStopId);
        Assert.Equal(3, new MediaListView(files[..3], null, 100).Shown);
    }

    /// <summary>A step of the keyboard says where it goes and how many files must be drawn to show it: a page past it, or all for End.</summary>
    [Fact]
    public void AStepOfTheKeyboardSaysWhereItGoesAndHowManyFilesMustBeDrawn()
    {
        List<MediaSummary> files = [.. Enumerable.Range(0, 450).Select(File)];
        MediaListView view = new(files, null, 100);

        (MediaSummary target, int draw) = view.Go("m99", MediaStep.Next)!.Value;
        (MediaSummary within, int staysAt) = view.Go("m10", MediaStep.Next)!.Value;
        (MediaSummary last, int all) = view.Go("m0", MediaStep.Last)!.Value;
        (MediaSummary first, _) = view.Go("m50", MediaStep.First)!.Value;
        (MediaSummary above, _) = view.Go("m0", MediaStep.Previous)!.Value;

        Assert.Equal(("m100", 200), (target.Id, draw));
        Assert.Equal(("m11", 100), (within.Id, staysAt));
        Assert.Equal(("m449", 450), (last.Id, all));
        Assert.Equal(("m0", "m0"), (first.Id, above.Id));
        Assert.Null(view.Go("gone", MediaStep.Next));
    }
}
