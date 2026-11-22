using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Mutes or unmutes every member of a group.</summary>
public sealed record SetGroupMuted : Command
{
    /// <summary>The group to edit.</summary>
    [Description("The group to edit.")]
    public required GroupId Group { get; init; }

    /// <summary>True to silence every member.</summary>
    [Description("True to silence every member.")]
    public required bool Muted { get; init; }
}
