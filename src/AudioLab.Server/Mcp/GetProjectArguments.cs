using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Server.Mcp;

/// <summary>The input of get_project: which project, and which part of it.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GetProjectArguments
{
    /// <summary>The id of one of your projects, as list_projects gives it.</summary>
    [Description("The id of one of your projects, as list_projects gives it.")]
    public required ProjectId Project { get; init; }

    /// <summary>The part to read; the summary when it is left out.</summary>
    [Description("The part to read. summary (the default) counts what the project holds and lists the tracks with their ids. "
        + "audio_clips and midi_clips need a track; notes and control_changes need a track and a clip.")]
    public ProjectSection? Section { get; init; }

    /// <summary>The track that tracks, audio_clips, midi_clips, notes and control_changes read from.</summary>
    [Description("The id of a track, from the summary. Needed by audio_clips, midi_clips, notes and control_changes; narrows tracks to one.")]
    public TrackId? Track { get; init; }

    /// <summary>The clip that notes and control_changes read from.</summary>
    [Description("The id of a MIDI clip. Needed by notes and control_changes; narrows audio_clips and midi_clips to one.")]
    public ClipId? Clip { get; init; }

    /// <summary>How many items to skip in a list section.</summary>
    [Description("How many items to skip at the start of a list (0 by default).")]
    [Range(0, int.MaxValue)]
    public int? Offset { get; init; }

    /// <summary>The most items to return from a list section.</summary>
    [Description("The most items to return from a list; all of them by default. The answer says the total, so a long list is read in parts.")]
    [Range(0, int.MaxValue)]
    public int? Count { get; init; }
}
