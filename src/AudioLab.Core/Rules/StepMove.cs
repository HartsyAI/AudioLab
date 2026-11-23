using AudioLab.Core.Model;

namespace AudioLab.Core.Rules;

/// <summary>Moving one item of an ordered list a place up or down, which the effect chain and the drum lanes share.</summary>
public static class StepMove
{
    /// <summary>The list with the item at <paramref name="from"/> swapped with its neighbour in <paramref name="direction"/>.</summary>
    /// <remarks>An item at that end, or an index outside the list, leaves the list as it was (the same instance).</remarks>
    public static ValueList<T> Swap<T>(ValueList<T> items, int from, MoveDirection direction)
    {
        ArgumentNullException.ThrowIfNull(items);
        int to = from + (direction == MoveDirection.Down ? 1 : -1);
        if (from < 0 || from >= items.Count || to < 0 || to >= items.Count)
        {
            return items;
        }
        List<T> list = [.. items];
        (list[from], list[to]) = (list[to], list[from]);
        return [.. list];
    }
}
