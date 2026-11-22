using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Takes every track out of a group and removes the group.</summary>
public sealed record Ungroup : Command
{
    /// <summary>The group to dissolve.</summary>
    [Description("The group to dissolve.")]
    public required GroupId Group { get; init; }
}
