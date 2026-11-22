using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a tempo change; the earlier tempo carries on.</summary>
public sealed record RemoveTempoChange : Command
{
    /// <summary>The tempo change to remove.</summary>
    public required TempoChangeId Change { get; init; }
}
