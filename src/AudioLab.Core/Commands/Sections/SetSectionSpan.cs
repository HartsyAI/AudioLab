using System.ComponentModel;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves a section or one of its edges, kept off its neighbours.</summary>
public sealed record SetSectionSpan : Command
{
    /// <summary>The section to edit.</summary>
    [Description("The section to edit.")]
    public required SectionId Section { get; init; }

    /// <summary>The requested start; required to move the section or its start edge.</summary>
    [Description("The requested start; required to move the section or its start edge.")]
    public double? Start { get; init; }

    /// <summary>The requested end; required to move its end edge.</summary>
    [Description("The requested end; required to move its end edge.")]
    public double? End { get; init; }

    /// <summary>Whether the whole section moves (move) or one edge (start, end).</summary>
    [Description("Whether the whole section moves (move) or one edge (start, end).")]
    public required SectionEdit Edit { get; init; }
}
