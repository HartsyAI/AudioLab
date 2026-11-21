using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>Sections and groups, cleaned up the way the extension cleans them when it opens a project.</summary>
internal static class V3Sections
{
    /// <summary>Sections as the extension restores them: bad ones dropped, sorted, overlaps trimmed, gaps filled in.</summary>
    public static List<Section> ConvertSections(List<V3Section> raw, V3Context context)
    {
        List<SectionDraft> valid = [];
        for (int i = 0; i < raw.Count; i++)
        {
            bool longEnough = raw[i].End - raw[i].Start >= Section.MinLength - ProjectLimits.Tolerance;
            if (raw[i].Start is { } start && raw[i].End is { } end && start >= 0 && longEnough)
            {
                valid.Add(new SectionDraft(i, start, end, raw[i].Name, raw[i].Color));
            }
            else
            {
                string minimum = V3Context.Num(Section.MinLength);
                context.Warn($"sections[{i}] was shorter than {minimum} seconds or started before 0; it was dropped.");
            }
        }
        List<SectionDraft> placed = [];
        foreach (SectionDraft draft in valid.OrderBy(section => section.Start))
        {
            SectionDraft? previous = placed.Count > 0 ? placed[^1] : null;
            if (previous is not null && previous.End > draft.Start && !Trim(previous, draft, context))
            {
                continue;
            }
            placed.Add(draft);
        }
        return
        [
            .. placed.Select((draft, index) => new Section
            {
                Id = context.Ids.Next<SectionId>(),
                Start = draft.Start,
                End = draft.End,
                Name = string.IsNullOrWhiteSpace(draft.Name) ? $"Section {index + 1}" : draft.Name.Trim(),
                Color = context.Color(
                    draft.Color,
                    ProjectDefaults.SectionColor(index),
                    $"sections[{draft.Index}].color")!,
            }),
        ];
    }

    /// <summary>Groups as the extension restores them; every raw group gets a group, in order.</summary>
    public static List<TrackGroup> ConvertGroups(List<V3Group> raw, V3Context context)
    {
        List<TrackGroup> groups = [];
        HashSet<string> seen = [];
        for (int i = 0; i < raw.Count; i++)
        {
            V3Group group = raw[i];
            if (!string.IsNullOrEmpty(group.Id) && !seen.Add(group.Id))
            {
                context.Warn($"groups[{i}] repeats the id {group.Id}; it is a separate group with a new id.");
            }
            groups.Add(new TrackGroup
            {
                Id = context.Ids.Next<GroupId>(),
                Name = string.IsNullOrWhiteSpace(group.Name) ? UniqueName(groups) : group.Name,
                Color = context.Color(group.Color, ProjectDefaults.GroupColor, $"groups[{i}].color")!,
                Collapsed = group.Collapsed == true,
                Tint = group.Tint != false,
                Muted = group.Muted == true,
                Soloed = group.Soloed == true,
                Volume = Math.Max(0, group.Volume ?? 1),
            });
        }
        return groups;
    }

    /// <summary>The new id of each group by its old id; the first group with an id keeps it for the tracks naming it.</summary>
    public static Dictionary<string, GroupId> GroupIdsByOldId(List<V3Group> raw, List<TrackGroup> groups)
    {
        Dictionary<string, GroupId> byOldId = new(StringComparer.Ordinal);
        for (int i = 0; i < raw.Count; i++)
        {
            if (!string.IsNullOrEmpty(raw[i].Id))
            {
                byOldId.TryAdd(raw[i].Id!, groups[i].Id);
            }
        }
        return byOldId;
    }

    // The earlier section yields, unless that would leave it too short, then the later one yields.
    private static bool Trim(SectionDraft previous, SectionDraft draft, V3Context context)
    {
        if (draft.Start - previous.Start >= Section.MinLength)
        {
            previous.End = draft.Start;
        }
        else if (draft.End - previous.End >= Section.MinLength)
        {
            draft.Start = previous.End;
        }
        else
        {
            context.Warn($"sections[{draft.Index}] was swallowed by sections[{previous.Index}]; it was dropped.");
            return false;
        }
        context.Warn($"sections[{draft.Index}] overlapped sections[{previous.Index}]; one of them was trimmed.");
        return true;
    }

    private static string UniqueName(List<TrackGroup> groups)
    {
        HashSet<string> used = [.. groups.Select(group => group.Name)];
        for (int i = groups.Count + 1; ; i++)
        {
            string name = $"Group {i}";
            if (!used.Contains(name))
            {
                return name;
            }
        }
    }

    private sealed class SectionDraft(int index, double start, double end, string? name, string? color)
    {
        public int Index { get; } = index;

        public double Start { get; set; } = start;

        public double End { get; set; } = end;

        public string? Name { get; } = name;

        public string? Color { get; } = color;
    }
}
