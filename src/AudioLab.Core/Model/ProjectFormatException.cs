namespace AudioLab.Core.Model;

/// <summary>A project file or document that cannot be read or written, with every problem found.</summary>
public sealed class ProjectFormatException : Exception
{
    /// <summary>Creates the exception with a message and the individual problems, which it copies.</summary>
    public ProjectFormatException(string message, IReadOnlyList<string>? problems = null, Exception? inner = null)
        : base(Describe(message, problems), inner)
    {
        Problems = problems is null ? [] : [.. problems];
    }

    /// <summary>The individual problems, each with the path of the value it is about.</summary>
    public IReadOnlyList<string> Problems { get; }

    private static string Describe(string message, IReadOnlyList<string>? problems)
    {
        return problems is { Count: > 0 } ? $"{message} {string.Join("; ", problems)}" : message;
    }
}
