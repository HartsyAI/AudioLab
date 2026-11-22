using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The section commands; sections never overlap and stay in time order.</summary>
internal static class SectionCommands
{
    // What a new section covers when it is not dragged out.
    private const double DefaultBars = 4;

    public static void Register(CommandRegistryBuilder builder, TimelineCommandJsonContext json)
    {
        builder
            .Add("section_add", "Adds a section, cut short at the next one, and allocates its id.", json.AddSection, Add)
            .Add("section_set_span", "Moves a section or one edge of it, kept off its neighbours.", json.SetSectionSpan, SetSpan)
            .Add("section_rename", "Renames a section.", json.RenameSection, Rename)
            .Add("section_set_color", "Recolours a section.", json.SetSectionColor, SetColor)
            .Add("section_remove", "Removes a section.", json.RemoveSection, Remove)
            .Add("section_clear", "Removes every section.", json.ClearSections, Clear)
            .Add("section_from_markers", "Replaces every section with one per marker and allocates their ids in order.", json.SectionsFromMarkers, FromMarkers);
    }

    private static CommandResult Add(ProjectDocument document, AddSection command, IIdService ids)
    {
        if (CommandChecks.Finite((nameof(command.Start), command.Start), (nameof(command.Length), command.Length)) is { } refusal)
        {
            return refusal;
        }
        if (command.Start < 0)
        {
            return CommandChecks.OutOfRange("A section starts at the beginning of the project or later.");
        }
        if (command.Length < Section.MinLength)
        {
            return CommandChecks.OutOfRange($"A section is at least {Section.MinLength} seconds long.");
        }
        if (CommandChecks.TooLong(command.Name) is { } tooLong)
        {
            return tooLong;
        }
        double start = command.Start;
        double length = command.Length ?? TempoMap.From(document).BarsFrom(start, DefaultBars, document.TimeSignature);
        if (SectionRules.Fit(document.Sections, start, length) is not { } room)
        {
            return CommandChecks.Rejected("There is no room for a section here.");
        }
        Section added = new()
        {
            Id = ids.Next<SectionId>(),
            Start = room.Start,
            End = room.End,
            Name = string.IsNullOrWhiteSpace(command.Name) ? SectionRules.UniqueName(document.Sections) : command.Name.Trim(),
            Color = command.Color ?? ProjectDefaults.SectionColor(document.Sections.Count),
        };
        ValueList<Section> sections = [.. document.Sections.Append(added).OrderBy(section => section.Start)];
        return CommandResult.Ok(document with { Sections = sections }, added.Id.Value);
    }

    private static CommandResult SetSpan(ProjectDocument document, SetSectionSpan command, IIdService ids)
    {
        if (CommandChecks.Finite((nameof(command.Start), command.Start), (nameof(command.End), command.End)) is { } refusal)
        {
            return refusal;
        }
        Section? current = document.Sections.FirstOrDefault(section => section.Id == command.Section);
        if (current is null)
        {
            return CommandChecks.NotFound("section", command.Section.Value);
        }
        if (command.Edit != SectionEdit.End && command.Start is null)
        {
            return CommandChecks.OutOfRange("start is required to move a section or its start edge.");
        }
        if (command.Edit == SectionEdit.End && command.End is null)
        {
            return CommandChecks.OutOfRange("end is required to move a section's end edge.");
        }
        SectionSpan span = SectionRules.Constrain(
            document.Sections, command.Section, command.Start ?? current.Start, command.End ?? current.End, command.Edit);
        if (Math.Abs(current.Start - span.Start) < TimelineRules.NoMove && Math.Abs(current.End - span.End) < TimelineRules.NoMove)
        {
            return CommandResult.Ok(document);
        }
        return Replace(document, command.Section, section => section with { Start = span.Start, End = span.End });
    }

    private static CommandResult Rename(ProjectDocument document, RenameSection command, IIdService ids)
    {
        if (CommandChecks.Named(command.Name) is { } refusal)
        {
            return refusal;
        }
        return Replace(document, command.Section, section => section with { Name = command.Name.Trim() });
    }

    private static CommandResult SetColor(ProjectDocument document, SetSectionColor command, IIdService ids)
    {
        return Replace(document, command.Section, section => section with { Color = command.Color });
    }

    private static CommandResult Remove(ProjectDocument document, RemoveSection command, IIdService ids)
    {
        if (!document.Sections.Any(section => section.Id == command.Section))
        {
            return CommandChecks.NotFound("section", command.Section.Value);
        }
        ValueList<Section> sections = [.. document.Sections.Where(section => section.Id != command.Section)];
        return CommandResult.Ok(document with { Sections = sections });
    }

    private static CommandResult Clear(ProjectDocument document, ClearSections command, IIdService ids)
    {
        return CommandResult.Ok(document with { Sections = [] });
    }

    private static CommandResult FromMarkers(ProjectDocument document, SectionsFromMarkers command, IIdService ids)
    {
        if (document.Markers.Count == 0)
        {
            return CommandChecks.Rejected("Add markers first: each one starts a section.");
        }
        double last = document.Markers.Max(marker => marker.Time);
        double content = ProjectContent.End(document);
        double end = content > last + Section.MinLength
            ? content
            : last + TempoMap.From(document).BarsFrom(last, DefaultBars, document.TimeSignature);
        IReadOnlyList<SectionDraft> drafts = SectionRules.FromMarkers(document.Markers, end);
        if (drafts.Count == 0)
        {
            return CommandChecks.Rejected("No marker leaves room for a section.");
        }
        List<Section> made = [];
        foreach (SectionDraft draft in drafts)
        {
            made.Add(new Section { Id = ids.Next<SectionId>(), Start = draft.Start, End = draft.End, Name = draft.Name, Color = draft.Color });
        }
        ValueList<string> created = [.. made.Select(section => section.Id.Value)];
        return CommandResult.Ok(document with { Sections = new ValueList<Section>(made) }, created);
    }

    private static CommandResult Replace(ProjectDocument document, SectionId id, Func<Section, Section> edit)
    {
        if (!document.Sections.Any(section => section.Id == id))
        {
            return CommandChecks.NotFound("section", id.Value);
        }
        ValueList<Section> sections = [.. document.Sections.Select(section => section.Id == id ? edit(section) : section)];
        return CommandResult.Ok(document with { Sections = sections });
    }
}
