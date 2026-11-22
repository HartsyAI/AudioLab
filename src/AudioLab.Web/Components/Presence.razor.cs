using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue presence: overlapping round avatars for everyone in the session; an agent takes a track colour and the label AI.</summary>
public sealed partial class Presence
{
    /// <summary>Everyone in the session.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<PresenceMember> Members { get; set; } = [];

    private static string Style(PresenceMember member)
    {
        return member.TrackColor is int color ? $"--avatar: var(--track-{color}); --avatar-ink: var(--ink)" : string.Empty;
    }
}
