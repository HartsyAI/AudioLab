using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>The section rules: sections never overlap, each is at least <see cref="Section.MinLength"/> long, and the list is in order.</summary>
public static class SectionRules
{
    /// <summary>The section that holds <paramref name="time"/> (start included, end excluded), or null.</summary>
    public static Section? At(IEnumerable<Section> sections, double time)
    {
        ArgumentNullException.ThrowIfNull(sections);
        return sections.FirstOrDefault(section => time >= section.Start && time < section.End);
    }

    /// <summary>The section that starts after <paramref name="time"/> (<paramref name="direction"/> above 0) or before it (below 0).</summary>
    public static Section? Neighbor(IEnumerable<Section> sections, double time, int direction, double tolerance = 1e-3)
    {
        ArgumentNullException.ThrowIfNull(sections);
        List<Section> ordered = [.. sections.OrderBy(section => section.Start)];
        return direction > 0
            ? ordered.FirstOrDefault(section => section.Start > time + tolerance)
            : ordered.LastOrDefault(section => section.Start < time - tolerance);
    }

    /// <summary>Where a dragged section may go: the span asked for, kept off its neighbours.</summary>
    /// <remarks>A request that does not fit leaves the section where it is. The UI previews with this and the server applies it, so both land on the same span.</remarks>
    /// <param name="sections">The current sections.</param>
    /// <param name="id">The section being dragged.</param>
    /// <param name="start">The requested start; used by <see cref="SectionEdit.Move"/> and <see cref="SectionEdit.Start"/>.</param>
    /// <param name="end">The requested end; used by <see cref="SectionEdit.End"/>.</param>
    /// <param name="edit">Whether the whole section moves or one edge.</param>
    public static SectionSpan Constrain(IReadOnlyList<Section> sections, SectionId id, double start, double end, SectionEdit edit)
    {
        ArgumentNullException.ThrowIfNull(sections);
        Section? current = sections.FirstOrDefault(section => section.Id == id);
        if (current is null)
        {
            return new SectionSpan(start, end);
        }
        // Neighbours are judged around the current span, so a drag stops at them instead of leaping over.
        (double low, double high) = FreeGap(sections, id, current.Start, current.End);
        double length = current.End - current.Start;
        switch (edit)
        {
            case SectionEdit.Move:
                if (high - low < length)
                {
                    return new SectionSpan(current.Start, current.End);
                }
                double moved = Math.Max(low, Math.Min(start, high - length));
                return new SectionSpan(moved, moved + length);
            case SectionEdit.Start:
                return new SectionSpan(Math.Max(low, Math.Min(start, current.End - Section.MinLength)), current.End);
            default:
                return new SectionSpan(current.Start, Math.Min(high, Math.Max(end, current.Start + Section.MinLength)));
        }
    }

    /// <summary>The room for a new section that starts at <paramref name="start"/> and runs <paramref name="length"/> seconds, cut short at the next section.</summary>
    /// <remarks>Null when the start is inside a section or the gap is too short.</remarks>
    public static SectionSpan? Fit(IReadOnlyList<Section> sections, double start, double length)
    {
        ArgumentNullException.ThrowIfNull(sections);
        if (!double.IsFinite(start) || !double.IsFinite(length) || start < 0 || At(sections, start) is not null)
        {
            return null;
        }
        Section? next = sections.OrderBy(section => section.Start).FirstOrDefault(section => section.Start > start);
        double end = Math.Min(start + Math.Max(length, Section.MinLength), next?.Start ?? double.PositiveInfinity);
        return end - start >= Section.MinLength - ProjectLimits.Tolerance ? new SectionSpan(start, end) : null;
    }

    /// <summary>A name like "Section 3" that no section uses yet.</summary>
    public static string UniqueName(IReadOnlyList<Section> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        HashSet<string> used = [.. sections.Select(section => section.Name)];
        for (int number = sections.Count + 1; ; number++)
        {
            string name = $"Section {number.ToString(CultureInfo.InvariantCulture)}";
            if (!used.Contains(name))
            {
                return name;
            }
        }
    }

    /// <summary>A section per marker: each runs to the next marker and the last to <paramref name="endTime"/>.</summary>
    /// <remarks>A marker's name and colour carry over. Markers too close to the next one to make a section are skipped.</remarks>
    public static IReadOnlyList<SectionDraft> FromMarkers(IEnumerable<Marker> markers, double endTime)
    {
        ArgumentNullException.ThrowIfNull(markers);
        List<Marker> ordered = [.. markers.Where(marker => double.IsFinite(marker.Time) && marker.Time >= 0).OrderBy(marker => marker.Time)];
        List<SectionDraft> drafts = [];
        for (int i = 0; i < ordered.Count; i++)
        {
            double start = ordered[i].Time;
            double end = i + 1 < ordered.Count ? ordered[i + 1].Time : endTime;
            if (!(end - start >= Section.MinLength))
            {
                continue;
            }
            string name = ordered[i].Name.Trim();
            string label = name.Length > 0 ? name : $"Section {(drafts.Count + 1).ToString(CultureInfo.InvariantCulture)}";
            drafts.Add(new SectionDraft(start, end, label, ordered[i].Color ?? ProjectDefaults.SectionColor(drafts.Count)));
        }
        return drafts;
    }

    // The nearest section ends around a span: the previous one's end and the next one's start.
    private static (double Low, double High) FreeGap(IReadOnlyList<Section> sections, SectionId id, double start, double end)
    {
        double low = 0;
        double high = double.PositiveInfinity;
        double middle = (start + end) / 2;
        foreach (Section other in sections.Where(section => section.Id != id))
        {
            if (other.End <= middle)
            {
                low = Math.Max(low, other.End);
            }
            else if (other.Start >= middle)
            {
                high = Math.Min(high, other.Start);
            }
        }
        return (low, high);
    }
}
