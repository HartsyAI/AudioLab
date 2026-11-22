namespace AudioLab.Core.Model;

/// <summary>An enum converter that can list the exact names it accepts, so a schema can spell them out.</summary>
internal interface INamedEnumConverter
{
    /// <summary>The names the converter reads and writes, in declaration order.</summary>
    public IReadOnlyList<string> Names { get; }
}
