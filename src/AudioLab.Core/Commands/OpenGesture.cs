using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>A gesture that is still collecting commands: whose it is, and the document and label its undo step will carry.</summary>
/// <param name="Client">The client that owns the gesture.</param>
/// <param name="Id">The gesture's id, unique only among that client's gestures.</param>
/// <param name="Label">The wire name of the command that began it.</param>
/// <param name="Before">The document before its first change.</param>
internal readonly record struct OpenGesture(ClientId Client, GestureId Id, string Label, ProjectDocument Before);
