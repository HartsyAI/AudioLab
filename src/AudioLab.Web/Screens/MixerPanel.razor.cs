using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The mixer tab: one channel strip for each track, then the master.</summary>
public sealed partial class MixerPanel
{
    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    /// <summary>The selected track, or null.</summary>
    [Parameter]
    public TrackId? Selected { get; set; }

    [Inject]
    private TrackColorMap Colors { get; set; } = default!;

    private IReadOnlyList<(string Name, bool Enabled)> MasterEffects => Document.MasterLimiter ? [("Limiter", true)] : [];

    private static IReadOnlyList<(string Name, bool Enabled)> Effects(Track track)
    {
        return [.. track.Effects.Select(effect => (TrackText.Effect(effect), effect.Enabled))];
    }
}
