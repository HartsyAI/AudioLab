using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The mixer tab: a group's strip before its first track, one strip for each track, then the master and the sends.</summary>
public sealed partial class MixerPanel
{
    // The id is per instance, so two mixers on one page do not share it.
    private readonly string _id = $"mixer-{Guid.NewGuid():N}";

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

    [Inject]
    private IKeyGuard Guard { get; set; } = default!;

    [Inject]
    private AutomationRecorder Recorder { get; set; } = default!;

    // A touch or latch pass that records this track's volume takes the gain of a grab into the lane, not into the track's fader. The keys and the
    // reset that are not part of a grab still send, and so does a release that did not move the value (nothing of it reaches here then).
    private async Task SetTrackVolumeAsync((TrackId Track, double Gain) change)
    {
        bool grabbed = Recorder.RecordsLane(change.Track, AutomationParameter.Volume) && (Recorder.Holding || Recorder.TakeSettling());
        if (!grabbed)
        {
            await Actions.SetVolume(change.Track, change.Gain);
        }
    }

    private Task GrabAsync((TrackId Track, double Gain) grab)
    {
        Recorder.Grab(grab.Track, AutomationParameter.Volume, grab.Gain);
        return Task.CompletedTask;
    }

    private Task DragAsync((TrackId Track, double Gain) drag)
    {
        Recorder.Move(drag.Track, drag.Gain);
        return Task.CompletedTask;
    }

    private Task LetGoAsync((TrackId Track, double Gain, bool Sends) release)
    {
        Recorder.Release(release.Track, release.Gain, release.Sends);
        return Task.CompletedTask;
    }

    // A pointer grab taken down drops the pass it fed, as Escape does on a lane; a pass on another lane goes on.
    private Task CancelAsync(TrackId track)
    {
        if (Recorder.RecordsLane(track, AutomationParameter.Volume))
        {
            Recorder.Drop();
        }
        return Task.CompletedTask;
    }

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

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // A knob or a fader takes the arrow keys, Home and End; without this the transport would also seek with them.
        // Blazor calls this only once the components are drawn, so the guard is asked once, on the first draw.
        if (firstRender)
        {
            Guard.PreventNavigationKeys(_id, "slider");
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
            MixSilence.None when MixRules.IsHeardThroughBus(Document, track) => "Heard through a soloed bus",
            _ => null,
        };
    }

    private void OpenEffects((TrackId Track, EffectId? Effect) target) => Navigator.Open(target.Track, target.Effect);
}
