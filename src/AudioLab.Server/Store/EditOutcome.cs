using AudioLab.Core.Commands;

namespace AudioLab.Server.Store;

/// <summary>What an edit did to an open project.</summary>
/// <param name="Result">The command's result, with the ids it allocated or the reasons it was refused; null for an undo or a redo.</param>
/// <param name="Snapshot">The project after the edit; the same snapshot as before when nothing changed.</param>
/// <param name="Changed">Whether the edit raised the revision, so clients have something to hear.</param>
/// <param name="Json">The new document as <c>ProjectJson</c> writes it, serialised once for every client; null when nothing changed.</param>
public sealed record EditOutcome(CommandResult? Result, ProjectSnapshot Snapshot, bool Changed, string? Json);
