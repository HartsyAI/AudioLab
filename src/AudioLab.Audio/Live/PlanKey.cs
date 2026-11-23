using AudioLab.Core.Model;

namespace AudioLab.Audio.Live;

/// <summary>The part of a document a render plan is made from: documents with the same key sound the same, so need no new plan.</summary>
/// <remarks>
/// Left out: the project's name, the metronome setting (the click is played apart from the plan), the view, markers and sections; for a track its
/// name, colour, icon, arming, monitoring, input, take count and view; for a clip its name, colour and metadata (lyrics, score, stem links); for a
/// group its name, colour, collapsed state and tint. Kept, since the plan reads them: tempo and meter, master, the drum pattern (not rendered yet,
/// D35, but it will be),
/// and every other field. A field added to the model is kept until it is listed here.
/// </remarks>
internal static class PlanKey
{
    private static readonly ProjectView _blankView = new() { Ruler = RulerMode.Beats, EditMode = EditMode.Slip, Grid = "1/4" };

    private static readonly TrackView _blankTrackView =
        new() { Height = 100, AutomationVisible = false, AutomationParameter = AutomationParameter.Volume };

    private static readonly TrackInput _blankInput = new(null, InputChannelMode.Mono);

    /// <summary>The document with everything a plan does not read blanked.</summary>
    public static ProjectDocument Of(ProjectDocument document)
    {
        return document with
        {
            Name = string.Empty,
            Metronome = false,
            View = _blankView,
            Markers = [],
            Sections = [],
            Groups = [.. document.Groups.Select(group => group with { Name = string.Empty, Color = string.Empty, Collapsed = false, Tint = false })],
            Tracks = [.. document.Tracks.Select(Of)],
        };
    }

    private static Track Of(Track track)
    {
        return track with
        {
            Name = string.Empty,
            Color = string.Empty,
            Icon = null,
            Armed = false,
            Monitor = false,
            Input = _blankInput,
            TakeCount = 0,
            View = _blankTrackView,
            Clips = [.. track.Clips.Select(clip => clip with { Name = string.Empty, Color = null, Meta = null })],
            MidiClips = [.. track.MidiClips.Select(clip => clip with { Name = string.Empty, Color = null })],
        };
    }
}
