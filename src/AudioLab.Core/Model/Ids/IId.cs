namespace AudioLab.Core.Model;

/// <summary>A typed identifier that is a single string on the wire.</summary>
/// <typeparam name="TSelf">The identifier type itself.</typeparam>
public interface IId<TSelf>
    where TSelf : struct, IId<TSelf>
{
    /// <summary>The identifier text.</summary>
    public string Value { get; }

    /// <summary>Wraps the text of an id that exists, such as one read from a file; new ids come from <see cref="IIdService"/>.</summary>
    /// <exception cref="ArgumentException">The text is not a well-formed id.</exception>
    public static abstract TSelf From(string value);
}
