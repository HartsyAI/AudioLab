using AudioLab.Core.Model;
using AudioLab.Web.Components;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
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

    [Inject]
    private EffectsNavigator Navigator { get; set; } = default!;

    private IReadOnlyList<StripEffect> MasterEffects => Document.MasterLimiter ? [new StripEffect(null, "Limiter", true)] : [];

    private static IReadOnlyList<StripEffect> Effects(Track track)
    {
        return [.. track.Effects.Select(effect => new StripEffect(effect.Id, TrackText.Effect(effect), effect.Enabled))];
    }

    private void OpenEffects((TrackId Track, EffectId? Effect) target) => Navigator.Open(target.Track, target.Effect);
}
