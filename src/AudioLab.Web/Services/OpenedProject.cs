using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>A project as the open route answered: the whole document and the revision it is.</summary>
/// <param name="Document">The document, read as strictly as the server reads a file.</param>
/// <param name="Revision">The revision of <paramref name="Document"/>; later changes carry a higher one.</param>
public sealed record OpenedProject(ProjectDocument Document, long Revision);
