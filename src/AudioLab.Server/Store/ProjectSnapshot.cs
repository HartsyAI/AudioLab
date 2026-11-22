using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>A project document and the revision it has; the revision rises by one with every change that alters the document.</summary>
/// <param name="Document">The immutable document.</param>
/// <param name="Revision">The revision, 1 when the project was opened.</param>
public sealed record ProjectSnapshot(ProjectDocument Document, long Revision);
