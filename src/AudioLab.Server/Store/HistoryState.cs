using System.Text.Json.Serialization;

namespace AudioLab.Server.Store;

/// <summary>What undo and redo hold for an open project, and the revision it is at.</summary>
/// <param name="Revision">The project's revision.</param>
/// <param name="UndoCount">How many steps undo can go back; a gesture still open is not counted.</param>
/// <param name="RedoCount">How many steps redo can go forward.</param>
/// <param name="UndoLabel">The wire name of the command whose step undo would take back; null when there is none.</param>
/// <param name="RedoLabel">The label of the step redo would put back; null when there is none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HistoryState(long Revision, int UndoCount, int RedoCount, string? UndoLabel, string? RedoLabel);
