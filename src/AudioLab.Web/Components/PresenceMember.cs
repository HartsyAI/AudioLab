namespace AudioLab.Web.Components;

/// <summary>One person or agent in the session, as an avatar.</summary>
/// <param name="Label">The short text in the avatar: You, or AI for an agent.</param>
/// <param name="Name">The full name for a screen reader.</param>
/// <param name="TrackColor">The 1-based track colour an agent takes, or null for the neutral avatar of a person.</param>
public readonly record struct PresenceMember(string Label, string Name, int? TrackColor = null);
