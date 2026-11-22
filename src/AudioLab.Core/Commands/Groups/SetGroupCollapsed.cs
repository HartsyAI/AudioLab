using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Folds a group to one row or shows its tracks.</summary>
public sealed record SetGroupCollapsed : Command
{
    /// <summary>The group to fold or show.</summary>
    [Description("The group to fold or show.")]
    public required GroupId Group { get; init; }

    /// <summary>True to fold the group away.</summary>
    [Description("True to fold the group away.")]
    public required bool Collapsed { get; init; }
}
