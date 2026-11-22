using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Moves a section or one of its edges, kept off its neighbours.</summary>
public sealed record SetSectionSpan : Command
{
    /// <summary>The section to edit.</summary>
    public required SectionId Section { get; init; }

    /// <summary>The requested start; used to move it or its start edge.</summary>
    public required double Start { get; init; }

    /// <summary>The requested end; used to move its end edge.</summary>
    public required double End { get; init; }

    /// <summary>Whether the whole section moves or one edge.</summary>
    public required SectionEdit Edit { get; init; }
}
