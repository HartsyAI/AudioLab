namespace AudioLab.Audio.Instruments;

/// <summary>A pool of voices, of a synth or a sampler, that plays the notes of an instrument track, for live playback and export alike.</summary>
/// <remarks>
/// <para>
/// The notes are worked out before the render (<see cref="NoteSchedule"/>) and every voice is built up front, so
/// <see cref="Render"/> allocates nothing, locks nothing and throws nothing. A note starts on its frame exactly; the output does not
/// depend on how the render is cut into blocks.
/// </para>
/// <para>
/// At most <see cref="MaxHeldVoices"/> notes sound at once, held or dying. A note that arrives with all of them in use takes over the
/// voice that is dying and quietest, or if none is dying the one that has been sounding longest (the lowest index on a tie), and that
/// voice fades out over 2 ms on a spare slot instead of cutting off. Should the spare slots too be in use, which takes more than
/// <see cref="PoolSize"/> - <see cref="MaxHeldVoices"/> takeovers within 2 ms, the fading voice nearest its end is cut off.
/// </para>
/// <para>
/// After a seek, or a first render, a note that began before the first frame and is still held starts there, told how much of it was
/// over, and the voice decides what that means for its sound.
/// </para>
/// </remarks>
internal sealed class PolyphonicSource : ITrackSource
{
    /// <summary>The most notes that sound at once, held or dying.</summary>
    public const int MaxHeldVoices = 32;

    /// <summary>The voices that exist, which includes those that are fading out after being taken over.</summary>
    public const int PoolSize = 48;

    private const int ScratchFrames = 256;

    private readonly NoteEvent[] _notes;
    private readonly InstrumentVoice[] _voices;
    private readonly float[] _left = new float[ScratchFrames];
    private readonly float[] _right = new float[ScratchFrames];
    private readonly bool _mono;
    private readonly Func<NoteEvent, long> _soundingEnd;
    private readonly double _tailSeconds;
    private readonly long _longestNote;
    private int _next;
    private long _expected;
    private bool _primed;

    /// <summary>Makes a source that plays the notes, which must be in the order <see cref="NoteSchedule"/> gives.</summary>
    /// <remarks>
    /// It builds <see cref="PoolSize"/> voices with <paramref name="makeVoice"/>; <paramref name="mono"/>: both channels match.
    /// <paramref name="soundingEnd"/> is the frame a note stops sounding, which a seek asks to see what is still playing; it is the note's
    /// release frame when null.
    /// </remarks>
    public PolyphonicSource(
        Func<InstrumentVoice> makeVoice,
        NoteEvent[] notes,
        bool mono,
        double tailSeconds,
        Func<NoteEvent, long>? soundingEnd = null)
    {
        _notes = notes;
        _mono = mono;
        _soundingEnd = soundingEnd ?? (note => note.OffFrame);
        _tailSeconds = tailSeconds;
        _voices = new InstrumentVoice[PoolSize];
        for (int i = 0; i < _voices.Length; i++)
        {
            _voices[i] = makeVoice();
        }
        foreach (NoteEvent note in notes)
        {
            _longestNote = Math.Max(_longestNote, _soundingEnd(note) - note.OnFrame);
        }
    }

    /// <inheritdoc />
    public bool IsMono => _mono;

    /// <inheritdoc />
    public double TailSeconds => _tailSeconds;

    /// <summary>How many voices are sounding, held, dying or fading.</summary>
    public int SoundingVoices => Count(held: true, dying: true, fading: true);

    /// <summary>How many voices are held or dying, which is what the polyphony limit counts.</summary>
    public int CountedVoices => Count(held: true, dying: true, fading: false);

    /// <summary>The start frame of the voice that has been held longest, or -1 when none is held.</summary>
    public long OldestHeldFrame
    {
        get
        {
            long oldest = -1;
            foreach (InstrumentVoice voice in _voices)
            {
                if (voice.State == VoiceState.Held && (oldest < 0 || voice.StartFrame < oldest))
                {
                    oldest = voice.StartFrame;
                }
            }
            return oldest;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        foreach (InstrumentVoice voice in _voices)
        {
            voice.Stop();
        }
        _primed = false;
    }

    /// <inheritdoc />
    public void Render(long startFrame, Span<float> left, Span<float> right)
    {
        if (!_primed || startFrame != _expected)
        {
            Prime(startFrame);
        }
        int done = 0;
        while (done < left.Length)
        {
            long frame = startFrame + done;
            while (_next < _notes.Length && _notes[_next].OnFrame <= frame)
            {
                Start(_notes[_next], frame, 0);
                _next++;
            }
            int chunk = Math.Min(left.Length - done, ScratchFrames);
            if (_next < _notes.Length)
            {
                chunk = (int)Math.Min(chunk, _notes[_next].OnFrame - frame);
            }
            Span<float> mixLeft = _left.AsSpan(0, chunk);
            Span<float> mixRight = _right.AsSpan(0, chunk);
            mixLeft.Clear();
            mixRight.Clear();
            foreach (InstrumentVoice voice in _voices)
            {
                voice.Render(frame, chunk, mixLeft, mixRight);
            }
            for (int i = 0; i < chunk; i++)
            {
                left[done + i] += mixLeft[i];
                right[done + i] += mixRight[i];
            }
            done += chunk;
        }
        _expected = startFrame + left.Length;
    }

    // A loop, not a query, because the render path calls it.
    private int Count(bool held, bool dying, bool fading)
    {
        int count = 0;
        foreach (InstrumentVoice voice in _voices)
        {
            if ((held && voice.State == VoiceState.Held) || (dying && voice.State == VoiceState.Released)
                || (fading && voice.State == VoiceState.Fading))
            {
                count++;
            }
        }
        return count;
    }

    // Finds where the notes are at the frame: the ones before it are over or chased, the first one at or after it is next.
    private void Prime(long frame)
    {
        Reset();
        _primed = true;
        int first = FirstStartingAt(frame - _longestNote);
        _next = FirstStartingAt(frame);
        for (int i = first; i < _next; i++)
        {
            if (_soundingEnd(_notes[i]) > frame)
            {
                Start(_notes[i], frame, frame - _notes[i].OnFrame);
            }
        }
    }

    private int FirstStartingAt(long frame)
    {
        int low = 0;
        int high = _notes.Length;
        while (low < high)
        {
            int middle = (low + high) >>> 1;
            if (_notes[middle].OnFrame < frame)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low;
    }

    private void Start(NoteEvent note, long frame, long elapsed)
    {
        if (CountedVoices >= MaxHeldVoices)
        {
            ChooseVictim().Take();
        }
        FreeSlot().Start(frame, note.OffFrame, note.Pitch, note.Velocity, elapsed);
    }

    // The quietest voice that is dying, else the one held longest; the lowest index wins a tie, so the choice never depends on luck.
    private InstrumentVoice ChooseVictim()
    {
        InstrumentVoice? dying = null;
        InstrumentVoice? oldest = null;
        foreach (InstrumentVoice voice in _voices)
        {
            if (voice.State == VoiceState.Released && (dying is null || voice.Level < dying.Level))
            {
                dying = voice;
            }
            else if (voice.State == VoiceState.Held && (oldest is null || voice.StartFrame < oldest.StartFrame))
            {
                oldest = voice;
            }
        }
        return dying ?? oldest!;
    }

    private InstrumentVoice FreeSlot()
    {
        InstrumentVoice? nearest = null;
        foreach (InstrumentVoice voice in _voices)
        {
            if (voice.State == VoiceState.Free)
            {
                return voice;
            }
            if (voice.State == VoiceState.Fading && (nearest is null || voice.Level < nearest.Level))
            {
                nearest = voice;
            }
        }
        return nearest!;
    }
}
