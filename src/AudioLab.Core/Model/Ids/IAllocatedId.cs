namespace AudioLab.Core.Model;

/// <summary>An identifier the server allocates through <see cref="IIdService"/>; clients never mint one.</summary>
/// <typeparam name="TSelf">The identifier type itself.</typeparam>
public interface IAllocatedId<TSelf> : IId<TSelf>
    where TSelf : struct, IAllocatedId<TSelf>
{
    /// <summary>The short prefix that tells ids of this kind apart in logs and JSON.</summary>
    public static abstract string Prefix { get; }
}
