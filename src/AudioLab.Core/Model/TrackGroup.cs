namespace AudioLab.Core.Model;

/// <summary>A named set of tracks with a shared mute, solo and volume. Groups do not nest.</summary>
public sealed record TrackGroup
{
    /// <summary>The group's id.</summary>
    public required GroupId Id { get; init; }

    /// <summary>The group's name.</summary>
    public required string Name { get; init; }

    /// <summary>The group's colour.</summary>
    public required string Color { get; init; }

    /// <summary>Whether the group is folded to one row in the arrangement.</summary>
    public required bool Collapsed { get; init; }

    /// <summary>Whether the members' clips draw in the group colour.</summary>
    public required bool Tint { get; init; }

    /// <summary>Whether every member is silent.</summary>
    public required bool Muted { get; init; }

    /// <summary>Whether every member counts as soloed.</summary>
    public required bool Soloed { get; init; }

    /// <summary>The gain multiplier applied to every member, from 0 up; 1 is unity.</summary>
    public required double Volume { get; init; }
}
