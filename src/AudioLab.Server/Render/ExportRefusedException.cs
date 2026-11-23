namespace AudioLab.Server.Render;

/// <summary>An export was refused; the message and the details are safe to show the client and name only the user's own things.</summary>
public sealed class ExportRefusedException : Exception
{
    /// <summary>Refuses an export, optionally listing what is wrong or missing.</summary>
    public ExportRefusedException(ExportRefusal reason, string message, IReadOnlyList<string>? details = null)
        : base(message)
    {
        Reason = reason;
        Details = details ?? [];
    }

    /// <summary>Why it was refused.</summary>
    public ExportRefusal Reason { get; }

    /// <summary>What is wrong or missing, such as the media ids of the project that are not there.</summary>
    public IReadOnlyList<string> Details { get; }
}
