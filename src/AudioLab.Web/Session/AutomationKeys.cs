using AudioLab.Web.Interop;
using AudioLab.Web.Services;

namespace AudioLab.Web.Session;

/// <summary>What a key does in an automation lane (D74): the decision, with no document or screen in it, so the rule is tested alone.</summary>
public static class AutomationKeys
{
    /// <summary>The decision for a key, given the lane's point count and the selected point.</summary>
    /// <param name="chord">The key press.</param>
    /// <param name="count">How many points the lane holds.</param>
    /// <param name="selected">The selected point's index, or null.</param>
    public static AutomationKeyAction Decide(KeyChord chord, int count, int? selected)
    {
        if (Shortcuts.AutomationPrevious.Matches(chord))
        {
            return Select(count == 0 ? null : Math.Max(0, (selected ?? count) - 1));
        }
        if (Shortcuts.AutomationNext.Matches(chord))
        {
            return Select(count == 0 ? null : Math.Min(count - 1, (selected ?? -1) + 1));
        }
        if (Shortcuts.AutomationAdd.Matches(chord))
        {
            // A repeat of Enter would stack points, and with nothing selected there is no place to add after.
            return !chord.Repeat && selected is { } at && at < count
                ? new AutomationKeyAction(AutomationKeyKind.Add, at)
                : new AutomationKeyAction(AutomationKeyKind.None);
        }
        if (Shortcuts.AutomationDelete.Matches(chord) && selected is { } index && index < count)
        {
            return new AutomationKeyAction(AutomationKeyKind.Delete, index);
        }
        if (selected is { } moved && moved < count)
        {
            NudgeDirection? direction = Nudge(chord);
            if (direction is { } chosen)
            {
                return new AutomationKeyAction(AutomationKeyKind.Nudge, moved, chosen);
            }
        }
        return new AutomationKeyAction(AutomationKeyKind.None);
    }

    private static NudgeDirection? Nudge(KeyChord chord)
    {
        if (Shortcuts.AutomationEarlier.Matches(chord))
        {
            return NudgeDirection.Earlier;
        }
        if (Shortcuts.AutomationLater.Matches(chord))
        {
            return NudgeDirection.Later;
        }
        if (Shortcuts.AutomationHigher.Matches(chord))
        {
            return NudgeDirection.Higher;
        }
        return Shortcuts.AutomationLower.Matches(chord) ? NudgeDirection.Lower : null;
    }

    private static AutomationKeyAction Select(int? index) => new(AutomationKeyKind.Select, index);
}

