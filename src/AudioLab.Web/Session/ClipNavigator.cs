using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>Moves the keyboard focus between clips: one tab stop, and the arrow keys walk the clips. Plain code, so a test drives it.</summary>
public static class ClipNavigator
{
    /// <summary>The clip a key moves to from <paramref name="current"/>, or null when the key moves nowhere.</summary>
    /// <param name="document">The arrangement.</param>
    /// <param name="current">The clip that has the focus.</param>
    /// <param name="key">The key as the browser names it: ArrowLeft, ArrowRight, ArrowUp, ArrowDown, Home or End.</param>
    public static ClipId? Next(ProjectDocument document, ClipId current, string key)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(key);
        List<List<(ClipId Id, double Start, double End)>> rows = Rows(document);
        int row = rows.FindIndex(clips => clips.Any(clip => clip.Id == current));
        if (row < 0)
        {
            return null;
        }
        List<(ClipId Id, double Start, double End)> own = rows[row];
        int index = own.FindIndex(clip => clip.Id == current);
        (ClipId id, double start, double end) = own[index];
        return key switch
        {
            "ArrowLeft" => index > 0 ? own[index - 1].Id : null,
            "ArrowRight" => index < own.Count - 1 ? own[index + 1].Id : null,
            "Home" => own[0].Id == current ? null : own[0].Id,
            "End" => own[^1].Id == current ? null : own[^1].Id,
            "ArrowUp" => Across(rows, row, -1, start, end),
            "ArrowDown" => Across(rows, row, 1, start, end),
            _ => null,
        };
    }

    /// <summary>The first clip of the arrangement in track order, or null when there is none; where the one tab stop sits at first.</summary>
    public static ClipId? First(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Rows(document).FirstOrDefault(row => row.Count > 0)?[0].Id;
    }

    // In the next row that has a clip, the clip that overlaps the start of this one, else the one whose start is nearest.
    private static ClipId? Across(List<List<(ClipId Id, double Start, double End)>> rows, int from, int step, double start, double end)
    {
        for (int row = from + step; row >= 0 && row < rows.Count; row += step)
        {
            if (rows[row].Count == 0)
            {
                continue;
            }
            foreach ((ClipId id, double clipStart, double clipEnd) in rows[row])
            {
                if (clipStart <= start && start < clipEnd)
                {
                    return id;
                }
            }
            return rows[row].MinBy(clip => Math.Abs(clip.Start - start)).Id;
        }
        return null;
    }

    private static List<List<(ClipId Id, double Start, double End)>> Rows(ProjectDocument document)
    {
        return
        [
            .. document.Tracks.Select(track => track.Clips.Select(clip => (clip.Id, clip.Start, ClipRules.EndOf(clip)))
                .Concat(track.MidiClips.Select(clip => (clip.Id, clip.Start, ClipRules.EndOf(clip))))
                .OrderBy(clip => clip.Item2)
                .ToList()),
        ];
    }
}
