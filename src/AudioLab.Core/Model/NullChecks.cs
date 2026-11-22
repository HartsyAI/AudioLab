namespace AudioLab.Core.Model;

/// <summary>Finds the members and list items a document must hold but that were set to null from C#.</summary>
/// <remarks>The compiler warns about this and the JSON reader refuses it, but a caller can still build such a document.</remarks>
internal static class NullChecks
{
    /// <summary>Records each null found with its path; true when there is none.</summary>
    public static bool AllPresent(ProjectDocument document, ProblemList problems)
    {
        int before = problems.Found;
        Present(document.Name, "name", problems);
        Present(document.View, "view", problems);
        Present(document.View?.Grid, "view.grid", problems);
        Items(document.Markers, "markers", problems, (marker, path) => Present(marker.Name, $"{path}.name", problems));
        Items(document.Sections, "sections", problems, SectionParts);
        Items(document.Groups, "groups", problems, GroupParts);
        Present(document.BeatPattern, "beatPattern", problems);
        Items(document.BeatPattern?.Lanes, "beatPattern.lanes", problems, LaneParts);
        Items(document.TempoMap, "tempoMap", problems);
        Items(document.Tracks, "tracks", problems, TrackParts);
        return problems.Found == before;

        void SectionParts(Section section, string path)
        {
            Present(section.Name, $"{path}.name", problems);
            Present(section.Color, $"{path}.color", problems);
        }

        void GroupParts(TrackGroup group, string path)
        {
            Present(group.Name, $"{path}.name", problems);
            Present(group.Color, $"{path}.color", problems);
        }

        void LaneParts(BeatLane lane, string path)
        {
            Present(lane.Name, $"{path}.name", problems);
            Present(lane.Steps, $"{path}.steps", problems);
        }

        void TrackParts(Track track, string path)
        {
            Present(track.Name, $"{path}.name", problems);
            Present(track.Color, $"{path}.color", problems);
            Present(track.View, $"{path}.view", problems);
            Present(track.Automation, $"{path}.automation", problems);
            Present(track.Automation?.Volume, $"{path}.automation.volume", problems);
            Present(track.Automation?.Pan, $"{path}.automation.pan", problems);
            Items(track.Sends, $"{path}.sends", problems);
            Items(track.Effects, $"{path}.effects", problems);
            Items(track.Clips, $"{path}.clips", problems, ClipParts);
            Items(track.MidiClips, $"{path}.midiClips", problems, MidiParts);
            InstrumentParts(track.Instrument, $"{path}.instrument");
        }

        void ClipParts(AudioClip clip, string path)
        {
            Present(clip.Name, $"{path}.name", problems);
            if (clip.Meta?.Lyrics is { } lyrics)
            {
                Present(lyrics.Text, $"{path}.meta.lyrics.text", problems);
                Items(lyrics.Words, $"{path}.meta.lyrics.words", problems);
            }
        }

        void MidiParts(MidiClip clip, string path)
        {
            Present(clip.Name, $"{path}.name", problems);
            Items(clip.Notes, $"{path}.notes", problems);
            Items(clip.ControlChanges, $"{path}.controlChanges", problems);
        }

        void InstrumentParts(Instrument? instrument, string path)
        {
            switch (instrument)
            {
                case SynthInstrument synth:
                    Present(synth.Preset, $"{path}.preset", problems);
                    break;
                case SamplerInstrument sampler:
                    Present(sampler.Name, $"{path}.name", problems);
                    Present(sampler.SampleName, $"{path}.sampleName", problems);
                    break;
            }
        }
    }

    private static void Present(object? value, string path, ProblemList problems)
    {
        if (value is null)
        {
            problems.Add($"{path}: is null; every member a document needs must be set.");
        }
    }

    private static void Items<T>(ValueList<T>? list, string path, ProblemList problems, Action<T, string>? each = null)
    {
        if (list is null)
        {
            problems.Add($"{path}: is null; every member a document needs must be set.");
            return;
        }
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] is not { } item)
            {
                problems.Add($"{path}[{i}]: is null; a list holds no null.");
            }
            else
            {
                each?.Invoke(item, $"{path}[{i}]");
            }
        }
    }
}
