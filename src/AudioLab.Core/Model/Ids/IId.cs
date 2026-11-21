namespace AudioLab.Core.Model;

/// <summary>A typed identifier that is a single string on the wire.</summary>
/// <typeparam name="TSelf">The identifier type itself.</typeparam>
public interface IId<TSelf>
    where TSelf : struct, IId<TSelf>
{
    /// <summary>The identifier text.</summary>
    public string Value { get; }

    /// <summary>Wraps identifier text; the caller has already checked it is not empty.</summary>
    public static abstract TSelf From(string value);
}
