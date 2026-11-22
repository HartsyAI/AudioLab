using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Puts tracks in a new group, taking them out of any other; the server allocates the group's id.</summary>
public sealed record CreateGroup : Command
{
    /// <summary>The tracks to group, from 1 to 1024.</summary>
    [Description("The tracks to group, from 1 to 1024.")]
    [JsonConverter(typeof(ValueListJsonConverter<TrackId>))]
    [MinLength(1)]
    [MaxLength(ProjectLimits.MaxTracks)]
    public required ValueList<TrackId> Tracks { get; init; }

    /// <summary>The group's name, up to 200 characters; "Group N" when absent or blank.</summary>
    [Description("The group's name, up to 200 characters; \"Group N\" when absent or blank.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Name { get; init; }

    /// <summary>Its colour as #rgb, #rrggbb or #rrggbbaa; the next track colour when absent.</summary>
    [Description("Its colour as #rgb, #rrggbb or #rrggbbaa; the next track colour when absent.")]
    [RegularExpression(ProjectColor.SchemaPattern)]
    public string? Color { get; init; }
}
