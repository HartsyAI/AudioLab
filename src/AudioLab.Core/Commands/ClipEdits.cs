using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Finding and replacing clips, which share one id space across audio and MIDI, for the clip handlers.</summary>
internal static class ClipEdits
{
    /// <summary>Where a clip is: its track, and its place among that track's audio clips or MIDI clips.</summary>
    public readonly record struct Location(int Track, int AudioIndex, int MidiIndex)
    {
        public bool IsAudio => AudioIndex >= 0;
    }

    /// <summary>The place of a clip of either kind, or null when no track holds it.</summary>
    public static Location? Find(ProjectDocument document, ClipId id)
    {
        for (int track = 0; track < document.Tracks.Count; track++)
        {
            for (int index = 0; index < document.Tracks[track].Clips.Count; index++)
            {
                if (document.Tracks[track].Clips[index].Id == id)
                {
                    return new Location(track, index, -1);
                }
            }
            for (int index = 0; index < document.Tracks[track].MidiClips.Count; index++)
            {
                if (document.Tracks[track].MidiClips[index].Id == id)
                {
                    return new Location(track, -1, index);
                }
            }
        }
        return null;
    }

    /// <summary>The refusal for a clip that no track holds.</summary>
    public static CommandResult NoClip(ClipId id) => CommandChecks.NotFound("clip", id.Value);

    /// <summary>The refusal for a command that only an audio clip, or only a MIDI clip, can take.</summary>
    public static CommandResult WrongKind(bool wantsAudio)
    {
        return CommandChecks.Rejected(wantsAudio ? "This command edits an audio clip." : "This command edits a MIDI clip.");
    }

    /// <summary>The document with one track replaced.</summary>
    public static ProjectDocument WithTrack(ProjectDocument document, int index, Func<Track, Track> edit)
    {
        ValueList<Track> tracks = [.. document.Tracks.Select((track, position) => position == index ? edit(track) : track)];
        return document with { Tracks = tracks };
    }

    /// <summary>Applies an edit to a clip of either kind.</summary>
    public static CommandResult Edit(ProjectDocument document, ClipId id, Func<AudioClip, AudioClip> audio, Func<MidiClip, MidiClip> midi)
    {
        if (Find(document, id) is not { } at)
        {
            return NoClip(id);
        }
        return CommandResult.Ok(WithTrack(document, at.Track, track => at.IsAudio
            ? track with { Clips = Replace(track.Clips, at.AudioIndex, audio) }
            : track with { MidiClips = Replace(track.MidiClips, at.MidiIndex, midi) }));
    }

    /// <summary>Applies an edit to an audio clip; a MIDI clip with that id is the wrong kind.</summary>
    public static CommandResult EditAudio(ProjectDocument document, ClipId id, Func<AudioClip, AudioClip> edit)
    {
        if (Find(document, id) is not { } at)
        {
            return NoClip(id);
        }
        return at.IsAudio
            ? CommandResult.Ok(WithTrack(document, at.Track, track => track with { Clips = Replace(track.Clips, at.AudioIndex, edit) }))
            : WrongKind(wantsAudio: true);
    }

    private static ValueList<T> Replace<T>(ValueList<T> list, int index, Func<T, T> edit)
    {
        return [.. list.Select((item, position) => position == index ? edit(item) : item)];
    }
}
