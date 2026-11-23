using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Services;

/// <summary>The edits of MIDI clips the session screen makes, each one command sent by wire name; the server's reasons come back as words.</summary>
public sealed class MidiEditor(LiveProject live)
{
    /// <summary>Adds an empty clip of one bar to an instrument track at <paramref name="start"/>, or after the track's last clip; its id is in the result.</summary>
    public Task<EditResult> AddClipAsync(TrackId track, double? start = null)
    {
        return EditRunner.RunAsync(live, project =>
        {
            ProjectDocument document = live.Document!;
            Track? target = document.Tracks.FirstOrDefault(candidate => candidate.Id == track);
            double at = Math.Max(0, start ?? (target is null ? 0 : MidiClipPlan.NextStart(document, target)));
            double length = MidiClipPlan.DefaultLength(document, at);
            return live.SendAsync(new AddMidiClip { Project = project, Track = track, Start = at, Length = length });
        });
    }

    /// <summary>Sets a clip's length to an absolute number of seconds.</summary>
    public Task<EditResult> SetLengthAsync(ClipId clip, double length)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetMidiClipLength { Project = project, Clip = clip, Length = length }));
    }

    /// <summary>Moves a clip's end by whole bars, through the tempo map; shortening it past the least length is refused with the reason.</summary>
    public async Task<EditResult> StepLengthAsync(ClipId clip, int bars)
    {
        if (live.Document is not { } document || MidiClipPlan.Find(document, clip) is not { } found)
        {
            return EditResult.Refused("That MIDI clip is not in the project.");
        }
        return MidiClipPlan.Stepped(document, found.Clip, bars) is { } length
            ? await SetLengthAsync(clip, length)
            : EditResult.Refused($"A MIDI clip is at least {MidiClip.MinLength} seconds long.");
    }
}
