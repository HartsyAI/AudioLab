using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a section.</summary>
public sealed record RemoveSection : Command
{
    /// <summary>The section to remove.</summary>
    [Description("The section to remove.")]
    public required SectionId Section { get; init; }
}
