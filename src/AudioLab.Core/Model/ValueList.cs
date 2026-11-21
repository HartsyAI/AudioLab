using System.Collections;
using System.Runtime.CompilerServices;

namespace AudioLab.Core.Model;

/// <summary>An immutable list equal to any other with the same items in order, so records holding one compare by value.</summary>
/// <typeparam name="T">The item type.</typeparam>
[CollectionBuilder(typeof(ValueList), nameof(ValueList.Create))]
public sealed class ValueList<T> : IReadOnlyList<T>, IEquatable<ValueList<T>>
{
    private readonly T[] _items;

    private int _hash;

    /// <summary>Copies <paramref name="items"/>, so later changes to the source never reach this list.</summary>
    public ValueList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = [.. items];
    }

    /// <summary>The list with no items.</summary>
    public static ValueList<T> Empty { get; } = new([]);

    /// <inheritdoc />
    public int Count => _items.Length;

    /// <inheritdoc />
    public T this[int index] => _items[index];

    /// <summary>Whether two lists have the same items in the same order.</summary>
    public static bool operator ==(ValueList<T>? left, ValueList<T>? right) => Equals(left, right);

    /// <summary>Whether two lists differ.</summary>
    public static bool operator !=(ValueList<T>? left, ValueList<T>? right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(ValueList<T>? other)
    {
        return other is not null && (ReferenceEquals(this, other) || _items.AsSpan().SequenceEqual(other._items));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValueList<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        if (_hash == 0)
        {
            HashCode hash = new();
            foreach (T item in _items)
            {
                hash.Add(item);
            }
            _hash = hash.ToHashCode() | 1;
        }
        return _hash;
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
