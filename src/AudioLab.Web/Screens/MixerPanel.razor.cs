using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The mixer tab: one channel strip for each track, a group's strip before its first track, then the master and the sends.</summary>
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

    private Track? SelectedTrack => Selected is { } id ? Document.Tracks.FirstOrDefault(track => track.Id == id) : null;

    private IReadOnlyList<StripEffect> MasterEffects => Document.MasterLimiter ? [new StripEffect(null, "Limiter", true)] : [];

    // A group's strip comes before its first track; a folded group draws its strip alone.
    private IReadOnlyList<MixerLane> Lanes
    {
        get
        {
            List<MixerLane> lanes = [];
            HashSet<GroupId> begun = [];
            foreach (Track track in Document.Tracks)
            {
                TrackGroup? group = GroupRules.GroupOf(track, Document.Groups);
                if (group is not null)
                {
                    if (begun.Add(group.Id))
                    {
                        lanes.Add(new MixerLane(group, null, GroupRules.MembersOf(group.Id, Document.Tracks).Count));
                    }
                    if (group.Collapsed)
                    {
                        continue;
                    }
                }
                lanes.Add(new MixerLane(null, track, 0));
            }
            return lanes;
        }
    }

    private static IReadOnlyList<StripEffect> Effects(Track track)
    {
        return [.. track.Effects.Select(effect => new StripEffect(effect.Id, TrackText.Effect(effect), effect.Enabled))];
    }

    // Why a track is silent, in words, so the strip says it and not only its colour; null when it is heard.
    private string? SilenceOf(Track track)
    {
        return MixRules.SilenceOf(Document, track) switch
        {
            MixSilence.TrackMuted => "Muted",
            MixSilence.GroupMuted => $"Muted by {GroupRules.GroupOf(track, Document.Groups)?.Name ?? "its group"}",
            MixSilence.SoloElsewhere => "Silenced by a solo elsewhere",
            _ => null,
        };
    }

    private void OpenEffects((TrackId Track, EffectId? Effect) target) => Navigator.Open(target.Track, target.Effect);
}
