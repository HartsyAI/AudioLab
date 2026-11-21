using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>Sections and groups, cleaned up the way the extension cleans them when it opens a project.</summary>
internal static class V3Sections
{
    private const string DefaultGroupColor = "#7a8cff";

    private static readonly string[] _palette =
    [
        "#f4a261", "#f2c94c", "#b5d63d", "#5fcf80", "#2ec4b6", "#4cc9f0", "#4895ef", "#9d7bf0", "#d97ae0", "#ef6f91",
    ];

    public static List<Section> ConvertSections(List<V3Section> raw, V3Context context)
    {
        List<SectionDraft> valid = [];
        foreach (V3Section section in raw)
        {
            if (section.Start is { } start && section.End is { } end && start >= 0 && end - start >= Section.MinLength - 1e-9)
            {
                valid.Add(new SectionDraft(start, end, section.Name, section.Color));
            }
            else
            {
                context.Warn("A section that was shorter than 0.1 seconds or started before 0 was dropped.");
            }
        }
        List<SectionDraft> placed = [];
        foreach (SectionDraft draft in valid.OrderBy(section => section.Start))
        {
            SectionDraft? previous = placed.Count > 0 ? placed[^1] : null;
            if (previous is not null && previous.End > draft.Start)
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
                    context.Warn("A section swallowed by the one before it was dropped.");
                    continue;
                }
                context.Warn("Overlapping sections were trimmed so none overlap.");
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
                Color = string.IsNullOrEmpty(draft.Color) ? _palette[index % _palette.Length] : draft.Color,
            }),
        ];
    }

    public static List<TrackGroup> ConvertGroups(List<V3Group> raw, V3Context context)
    {
        List<TrackGroup> groups = [];
        HashSet<string> seen = [];
        foreach (V3Group group in raw)
        {
            if (!string.IsNullOrEmpty(group.Id) && !seen.Add(group.Id))
            {
                context.Warn($"A second group with the id {group.Id} was kept as a separate group with a new id.");
            }
            groups.Add(new TrackGroup
            {
                Id = context.Ids.Next<GroupId>(),
                Name = string.IsNullOrWhiteSpace(group.Name) ? UniqueName(groups) : group.Name,
                Color = string.IsNullOrEmpty(group.Color) ? DefaultGroupColor : group.Color,
                Collapsed = group.Collapsed == true,
                Tint = group.Tint != false,
                Muted = group.Muted == true,
                Soloed = group.Soloed == true,
                Volume = Math.Max(0, group.Volume ?? 1),
            });
        }
        return groups;
    }

    // The first group with a given id keeps it for the tracks that name it; later ones get a fresh id nobody names.
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

    private sealed class SectionDraft(double start, double end, string? name, string? color)
    {
        public double Start { get; set; } = start;

        public double End { get; set; } = end;

        public string? Name { get; } = name;

        public string? Color { get; } = color;
    }
}
