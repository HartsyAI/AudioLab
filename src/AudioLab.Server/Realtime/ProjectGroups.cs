using AudioLab.Core.Model;

namespace AudioLab.Server.Realtime;

/// <summary>Names the hub group of a project. The owner is part of the name, so one user's clients can never be in another user's group.</summary>
public static class ProjectGroups
{
    /// <summary>The group of <paramref name="project"/> owned by <paramref name="owner"/>.</summary>
    public static string Of(UserId owner, ProjectId project) => $"project/{owner.Value}/{project.Value}";
}
