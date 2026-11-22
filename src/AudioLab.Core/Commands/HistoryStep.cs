using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>One undo or redo step: the document to go back to or forward to, and the command that began the step.</summary>
/// <param name="Label">The wire name of the command.</param>
/// <param name="Document">The document the step restores.</param>
internal readonly record struct HistoryStep(string Label, ProjectDocument Document);
