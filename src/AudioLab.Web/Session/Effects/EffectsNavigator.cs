using AudioLab.Core.Model;

namespace AudioLab.Web.Session.Effects;

/// <summary>Opens the effects tab on a track, and on one of its effects when there is one: the way from a channel strip to the chain.</summary>
public sealed class EffectsNavigator(SessionSelection selection, EffectSelection effects, BottomPanelState panel)
{
    /// <summary>Selects the track, selects the effect when one is given, asks for the keyboard focus to follow, and shows the effects tab.</summary>
    public void Open(TrackId track, EffectId? effect)
    {
        selection.SelectTrack(track);
        if (effect is { } chosen)
        {
            effects.Select(chosen);
        }
        effects.RequestFocus();
        panel.Show(BottomPanelState.Effects);
    }
}
