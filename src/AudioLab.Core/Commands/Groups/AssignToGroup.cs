using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Moves tracks into an existing group, or out of their group when none is given.</summary>
public sealed record AssignToGroup : Command
{
    /// <summary>The tracks to move; at least one.</summary>
    [Description("The tracks to move; at least one.")]
    [JsonConverter(typeof(ValueListJsonConverter<TrackId>))]
    [MinLength(1)]
    public required ValueList<TrackId> Tracks { get; init; }

    /// <summary>The group to move them into; absent to take them out of their group.</summary>
    [Description("The group to move them into; absent to take them out of their group.")]
    [JsonConverter(typeof(OptionalIdJsonConverter<GroupId>))]
    public GroupId? Group { get; init; }
}
