using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>Names an open project: the same project id under two users is two sessions.</summary>
/// <param name="User">The owner.</param>
/// <param name="Project">The project.</param>
public readonly record struct SessionKey(UserId User, ProjectId Project);
