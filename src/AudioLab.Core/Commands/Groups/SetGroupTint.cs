using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Chooses whether the group's clips draw in the group colour.</summary>
public sealed record SetGroupTint : Command
{
    /// <summary>The group to edit.</summary>
    [Description("The group to edit.")]
    public required GroupId Group { get; init; }

    /// <summary>True to draw the members' clips in the group colour.</summary>
    [Description("True to draw the members' clips in the group colour.")]
    public required bool Tint { get; init; }
}
