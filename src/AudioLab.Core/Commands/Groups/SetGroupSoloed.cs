using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Solos or unsolos every member of a group.</summary>
public sealed record SetGroupSoloed : Command
{
    /// <summary>The group to edit.</summary>
    [Description("The group to edit.")]
    public required GroupId Group { get; init; }

    /// <summary>True to count every member as soloed.</summary>
    [Description("True to count every member as soloed.")]
    public required bool Soloed { get; init; }
}
