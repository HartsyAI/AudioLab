using AudioLab.Core.Model;

namespace AudioLab.Web.Media;

/// <summary>The part of the library list that is drawn, and which file is its one tab stop, worked out once for each draw.</summary>
/// <remarks>
/// A list of thousands is drawn a page at a time: a row costs the page a component, so drawing every file would cost thousands of them on every
/// change. The tab stop is found by one scan, not by every row asking whether it is the one.
/// </remarks>
public sealed class MediaListView
{
    /// <summary>How many files are drawn at first, and how many more each step to the end of the page draws.</summary>
    public const int PageSize = 100;

    private readonly IReadOnlyList<MediaSummary> _files;

    /// <summary>Works out the view of <paramref name="files"/> with <paramref name="shown"/> of them drawn.</summary>
    /// <param name="files">The whole list, newest first.</param>
    /// <param name="selected">The id of the selected file, or null.</param>
    /// <param name="shown">How many files are drawn, at least one page.</param>
    public MediaListView(IReadOnlyList<MediaSummary> files, string? selected, int shown)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = files;
        Shown = Math.Clamp(Math.Max(shown, PageSize), 0, files.Count);
        List<MediaSummary> visible = new(Shown);
        for (int index = 0; index < Shown; index++)
        {
            visible.Add(files[index]);
        }
        Visible = visible;
        TabStopId = files.Count == 0 ? null : files[0].Id;
        if (selected is not null)
        {
            int at = IndexOf(selected);
            TabStopId = at >= 0 && at < Shown ? selected : TabStopId;
        }
    }

    /// <summary>How many files are drawn.</summary>
    public int Shown { get; }

    /// <summary>The files that are drawn.</summary>
    public IReadOnlyList<MediaSummary> Visible { get; }

    /// <summary>How many files are not drawn yet.</summary>
    public int Hidden => _files.Count - Shown;

    /// <summary>The id of the one row that is a tab stop: the selected file when it is drawn, else the first.</summary>
    public string? TabStopId { get; }

    /// <summary>The position of a file in the whole list, or -1.</summary>
    public int IndexOf(string id)
    {
        for (int index = 0; index < _files.Count; index++)
        {
            if (_files[index].Id == id)
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>The file a step of the keyboard goes to from <paramref name="from"/>, and how many files must be drawn to show it.</summary>
    /// <param name="from">The id of the file the step starts from.</param>
    /// <param name="step">Which way to go.</param>
    /// <returns>The target, or null when the step is not possible; and the count to draw, a whole page at least.</returns>
    public (MediaSummary Target, int Draw)? Go(string from, MediaStep step)
    {
        int at = IndexOf(from);
        if (at < 0)
        {
            return null;
        }
        int to = step switch
        {
            MediaStep.Next => Math.Min(at + 1, _files.Count - 1),
            MediaStep.Previous => Math.Max(at - 1, 0),
            MediaStep.First => 0,
            _ => _files.Count - 1,
        };
        int draw = step == MediaStep.Last ? _files.Count : Math.Max(Shown, ((to / PageSize) + 1) * PageSize);
        return (_files[to], Math.Min(draw, _files.Count));
    }
}
