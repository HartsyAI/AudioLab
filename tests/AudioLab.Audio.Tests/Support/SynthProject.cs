using AudioLab.Audio.Instruments;
using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests.Support;

/// <summary>Builds instrument tracks with MIDI clips and renders them through the synth and sampler factory.</summary>
public static class SynthProject
{
    /// <summary>A sine through an open filter at full sustain: its pitch and level are plain to measure.</summary>
    public static SynthInstrument Plain { get; } = InstrumentPresets.CreateSynth("Organ") with
    {
        Osc1 = OscillatorWave.Sine,
        Mix2 = 0,
        Detune = 0,
        Cutoff = 12000,
        Resonance = 0.3,
        FilterEnv = 0,
        Attack = 0.001,
        Decay = 0.01,
        Sustain = 1,
        Release = 0.05,
        Gain = 1,
    };

    public static MidiNote Note(int id, int pitch, double start, double duration, int velocity = 127) =>
        new(new NoteId(id), pitch, start, duration, velocity);

    public static MidiClip Clip(string id, double start, double length, MidiNote[] notes, params MidiControlChange[] controls) => new()
    {
        Id = ClipId.From(id),
        Name = id,
        Start = start,
        Length = length,
        NextNoteId = new NoteId(notes.Length + 1),
        Notes = new ValueList<MidiNote>(notes),
        ControlChanges = new ValueList<MidiControlChange>(controls),
        Muted = false,
    };

    public static Track InstrumentTrack(string id, Instrument instrument, params MidiClip[] clips) =>
        TestProject.Track(id, TrackKind.Instrument) with { Instrument = instrument, MidiClips = new ValueList<MidiClip>(clips) };

    public static ProjectDocument Project(params Track[] tracks) => TestProject.Create(tracks);

    public static ProjectDocument Project(SynthInstrument synth, params MidiClip[] clips) => Project(InstrumentTrack("t", synth, clips));

    public static RenderedAudio Render(
        ProjectDocument document,
        int rate = Rendering.Rate,
        int blockFrames = MixerGraph.DefaultBlockFrames,
        bool tail = true,
        MemoryMedia? media = null) =>
        OfflineRenderer.Render(
            document,
            media ?? new MemoryMedia(),
            new OfflineOptions
            {
                SampleRate = rate,
                BlockFrames = blockFrames,
                IncludeTail = tail,
                Sources = new InstrumentSourceFactory(),
            });

    /// <summary>The root mean square of the left channel between two times.</summary>
    public static double Rms(RenderedAudio audio, double from, double to, int rate = Rendering.Rate)
    {
        int start = Math.Clamp((int)Math.Round(from * rate), 0, audio.Frames);
        int end = Math.Clamp((int)Math.Round(to * rate), start, audio.Frames);
        double sum = 0;
        for (int i = start; i < end; i++)
        {
            sum += audio.Left[i] * (double)audio.Left[i];
        }
        return end == start ? 0 : Math.Sqrt(sum / (end - start));
    }

    public static float Peak(RenderedAudio audio, double from, double to, int rate = Rendering.Rate)
    {
        int start = Math.Clamp((int)Math.Round(from * rate), 0, audio.Frames);
        int end = Math.Clamp((int)Math.Round(to * rate), start, audio.Frames);
        float peak = 0;
        for (int i = start; i < end; i++)
        {
            peak = Math.Max(peak, Math.Abs(audio.Left[i]));
        }
        return peak;
    }

    /// <summary>The frequency of a tone from its upward zero crossings between two times.</summary>
    public static double Frequency(RenderedAudio audio, double from, double to, int rate = Rendering.Rate)
    {
        int start = (int)Math.Round(from * rate);
        int end = Math.Min(audio.Frames, (int)Math.Round(to * rate));
        int first = -1;
        int last = -1;
        int count = 0;
        for (int i = start + 1; i < end; i++)
        {
            if (audio.Left[i - 1] < 0 && audio.Left[i] >= 0)
            {
                last = i;
                first = first < 0 ? i : first;
                count++;
            }
        }
        return count < 2 ? 0 : (count - 1) * (double)rate / (last - first);
    }
}
