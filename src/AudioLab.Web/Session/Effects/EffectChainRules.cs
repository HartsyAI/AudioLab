using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session.Effects;

/// <summary>What the effect chain of the selected track allows and how it says so; the limits and their wording are Core's.</summary>
public static class EffectChainRules
{
    /// <summary>Why no effect can be added to the track now, or null when one can; the server's own words for a full chain.</summary>
    public static string? AddBlock(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return EffectRules.AddRefusal(track.Effects.Count);
    }

    /// <summary>Why the effect at <paramref name="index"/> cannot go one place in a direction, or null when it can.</summary>
    public static string? MoveBlock(int index, int count, MoveDirection direction)
    {
        if (direction == MoveDirection.Up)
        {
            return index <= 0 ? "It is already the first effect." : null;
        }
        return index >= count - 1 ? "It is already the last effect." : null;
    }

    /// <summary>How many effects a track has against the most it may have, such as "3 of 64".</summary>
    public static string Count(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return string.Create(CultureInfo.InvariantCulture, $"{track.Effects.Count} of {ProjectLimits.MaxEffects}");
    }

    /// <summary>Where an effect sits in its chain, such as "2 of 3", as a screen reader says it.</summary>
    public static string Position(int index, int count)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{index + 1} of {count}");
    }
}
