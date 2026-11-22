namespace AudioLab.Core.Model;

/// <summary>Builds <see cref="ValueList{T}"/> from collection expressions such as <c>[a, b]</c>.</summary>
public static class ValueList
{
    /// <summary>Called by the compiler for a collection expression.</summary>
    public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => ValueList<T>.Wrap(items.ToArray());
}
