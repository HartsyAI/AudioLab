namespace AudioLab.Server.Services;

/// <summary>What a save answers: the revision that is now on disk.</summary>
/// <param name="Id">The project's id.</param>
/// <param name="Revision">The saved revision.</param>
public sealed record SaveResult(string Id, long Revision);
