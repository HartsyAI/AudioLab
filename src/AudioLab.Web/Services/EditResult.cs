namespace AudioLab.Web.Services;

/// <summary>What an edit came to: applied, or refused with the server's reasons in a sentence for the user.</summary>
/// <param name="Succeeded">Whether the server applied it.</param>
/// <param name="Changed">Whether it changed the document; an edit that changes nothing succeeds and adds no undo step.</param>
/// <param name="Refusal">Why it was refused, or null when it succeeded.</param>
/// <param name="Created">The ids the server allocated for it.</param>
public readonly record struct EditResult(bool Succeeded, bool Changed, string? Refusal, IReadOnlyList<string> Created)
{
    /// <summary>An edit the server applied.</summary>
    public static EditResult Applied(bool changed, IReadOnlyList<string> created) => new(true, changed, null, created);

    /// <summary>An edit that was refused, or never reached the server.</summary>
    public static EditResult Refused(string reason) => new(false, false, reason, []);
}
