namespace AudioLab.Core.Model;

/// <summary>A project file or document that cannot be read, written or migrated, with every problem found.</summary>
public sealed class ProjectFormatException : Exception
{
    /// <summary>Creates the exception with a message and the individual problems.</summary>
    public ProjectFormatException(string message, IReadOnlyList<string>? problems = null, Exception? inner = null)
        : base(problems is { Count: > 0 } ? $"{message} {string.Join("; ", problems)}" : message, inner)
    {
        Problems = problems ?? [];
    }

    /// <summary>The individual problems, each with the path of the value it is about.</summary>
    public IReadOnlyList<string> Problems { get; }
}
