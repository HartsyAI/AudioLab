using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Adds a MIDI clip that already holds its notes and controller events, as one undo step; the server allocates the ids.</summary>
public sealed record ImportMidiClip : Command
{
    /// <summary>The instrument track to put the clip on.</summary>
    [Description("The instrument track to put the clip on.")]
    public required TrackId Track { get; init; }

    /// <summary>Where the clip starts on the timeline, in seconds.</summary>
    [Description("Where the clip starts on the timeline, in seconds.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public required double Start { get; init; }

    /// <summary>How long the clip is, in seconds, at least 0.05; every note starts and every event falls inside it.</summary>
    [Description("How long the clip is, in seconds, at least 0.05; every note starts and every event falls inside it.")]
    [Range(MidiClip.MinLength, ProjectLimits.MaxSeconds)]
    public required double Length { get; init; }

    /// <summary>The notes, which get the note ids 1, 2, 3 and so on in this order; at most 1,000,000.</summary>
    /// <remarks>
    /// Sent as JSON the clip is bounded by the limits of the hub (2 Mi characters) and MCP (1 MiB) on a command; the .mid route applies
    /// the command in process, so a file is bounded only by its own limit and the size of the project.
    /// </remarks>
    [Description(
        "The notes, which get the note ids 1, 2, 3 and so on in this order; at most 1,000,000. Sent as JSON the clip is bounded by the hub "
        + "and MCP limits on a command (2 Mi characters, 1 MiB); the .mid import route applies the command in process and is bounded "
        + "by its file limit and the project size.")]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required IReadOnlyList<ImportedNote> Notes { get; init; }

    /// <summary>The controller events, in time order, at most one for each time and controller; at most 1,000,000.</summary>
    [Description("The controller events, in time order, at most one for each time and controller; at most 1,000,000.")]
    [MaxLength(ProjectLimits.MaxListItems)]
    public required IReadOnlyList<ImportedControlChange> ControlChanges { get; init; }

    /// <summary>The clip's name, up to 200 characters; "MIDI N" when absent or blank.</summary>
    [Description("The clip's name, up to 200 characters; \"MIDI N\" when absent or blank.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Name { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa; absent to follow the track colour.</summary>
    [Description("A colour as #rgb, #rrggbb or #rrggbbaa; absent to follow the track colour.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public string? Color { get; init; }
}
