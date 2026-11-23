using AudioLab.Core.Model;

namespace AudioLab.Audio;

/// <summary>Keeps each media file cleaned and at one sample rate, so editing a project does not resample every file again.</summary>
/// <remarks>
/// A file is asked of the source once, a sample that is not a number becomes silence, and the result is kept for the next plan.
/// A media id always names the same audio, so entries are never refreshed; a changed file needs a new id, or <see cref="Remove"/>.
/// When the cached audio passes <c>maxBytes</c> the least recently used entries go, always keeping the newest; a plan that holds
/// audio keeps it alive. Safe to share between threads that prepare plans.
/// </remarks>
public sealed class MediaCache
{
    /// <summary>The default bound on the cached audio: 512 MiB of float samples.</summary>
    public const long DefaultMaxBytes = 512L * 1024 * 1024;

    // Output frames converted between two pauses of AddAsync: a few milliseconds of work in an ahead-of-time WebAssembly build.
    private const int SliceFrames = 8192;

    private readonly IMediaSource _source;
    private readonly long _maxBytes;
    private readonly Lock _lock = new();
    private readonly Dictionary<MediaId, LinkedListNode<Entry>> _entries = [];
    private readonly LinkedList<Entry> _recent = new();

    /// <summary>Creates a cache of the audio of <paramref name="source"/> at <paramref name="sampleRate"/>.</summary>
    public MediaCache(IMediaSource source, int sampleRate, long maxBytes = DefaultMaxBytes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, AudioFormat.MinSampleRate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, AudioFormat.MaxSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        _source = source;
        SampleRate = sampleRate;
        _maxBytes = maxBytes;
    }

    /// <summary>The rate every cached file is at.</summary>
    public int SampleRate { get; }

    /// <summary>How many bytes of audio the cache holds.</summary>
    public long Bytes { get; private set; }

    /// <summary>The audio of a media file at the cache's rate, or null when the source has none (that is not remembered).</summary>
    public PcmAudio? Get(MediaId id)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out LinkedListNode<Entry>? known))
            {
                _recent.Remove(known);
                _recent.AddFirst(known);
                return known.Value.Audio;
            }
        }
        if (!_source.TryGet(id, out PcmAudio? found))
        {
            return null;
        }
        return Store(id, Resampler.ToRate(Sanitise(found), SampleRate));
    }

    /// <summary>Keeps decoded audio that the caller already holds, cleaned and converted in slices with a pause between them.</summary>
    /// <remarks>For a thread that must stay responsive while it prepares media: a slice is a few milliseconds, and the source is not asked.</remarks>
    /// <param name="id">The media file the audio is.</param>
    /// <param name="decoded">The audio at its own rate.</param>
    /// <param name="pause">Awaited between slices.</param>
    /// <param name="cancellationToken">Stops the work; nothing is kept then.</param>
    public async Task<PcmAudio> AddAsync(MediaId id, PcmAudio decoded, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        ArgumentNullException.ThrowIfNull(pause);
        if (Get(id) is { } known)
        {
            return known;
        }
        PcmAudio clean = await SanitiseAsync(decoded, pause, cancellationToken);
        ResampleJob job = new(clean, SampleRate);
        while (!job.IsDone)
        {
            job.Run(SliceFrames);
            await pause(cancellationToken);
        }
        return Store(id, job.Result);
    }

    private PcmAudio Store(MediaId id, PcmAudio audio)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out LinkedListNode<Entry>? raced))
            {
                return raced.Value.Audio;
            }
            _entries[id] = _recent.AddFirst(new Entry(id, audio, BytesOf(audio)));
            Bytes += BytesOf(audio);
            Evict();
        }
        return audio;
    }

    // The audio itself when it is clean, found a slice at a time; the copy is made whole, which only happens for a damaged file.
    private static async Task<PcmAudio> SanitiseAsync(PcmAudio audio, Func<CancellationToken, Task> pause, CancellationToken cancellationToken)
    {
        for (int channel = 0; channel < audio.ChannelCount; channel++)
        {
            for (int at = 0; at < audio.Frames; at += SliceFrames * 8)
            {
                ReadOnlySpan<float> slice = audio.Channel(channel).Slice(at, Math.Min(SliceFrames * 8, audio.Frames - at));
                if (!AllFinite(slice))
                {
                    return Sanitise(audio);
                }
                await pause(cancellationToken);
            }
        }
        return audio;
    }

    private static bool AllFinite(ReadOnlySpan<float> samples)
    {
        foreach (float sample in samples)
        {
            if (!float.IsFinite(sample))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Forgets a file, so that the next plan asks the source for it again.</summary>
    public void Remove(MediaId id)
    {
        lock (_lock)
        {
            if (_entries.Remove(id, out LinkedListNode<Entry>? node))
            {
                _recent.Remove(node);
                Bytes -= node.Value.Bytes;
            }
        }
    }

    private static long BytesOf(PcmAudio audio) => (long)audio.Frames * audio.ChannelCount * sizeof(float);

    // The audio itself when it is clean, and a copy with silence in place of anything that is not a number when it is not.
    private static PcmAudio Sanitise(PcmAudio audio)
    {
        bool clean = true;
        for (int channel = 0; channel < audio.ChannelCount && clean; channel++)
        {
            foreach (float sample in audio.Channel(channel))
            {
                if (!float.IsFinite(sample))
                {
                    clean = false;
                    break;
                }
            }
        }
        if (clean)
        {
            return audio;
        }
        float[][] copy = new float[audio.ChannelCount][];
        for (int channel = 0; channel < copy.Length; channel++)
        {
            ReadOnlySpan<float> source = audio.Channel(channel);
            copy[channel] = new float[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                copy[channel][i] = float.IsFinite(source[i]) ? source[i] : 0;
            }
        }
        return new PcmAudio(audio.SampleRate, copy);
    }

    private void Evict()
    {
        while (Bytes > _maxBytes && _recent.Count > 1)
        {
            LinkedListNode<Entry> oldest = _recent.Last!;
            _recent.RemoveLast();
            _entries.Remove(oldest.Value.Id);
            Bytes -= oldest.Value.Bytes;
        }
    }

    private sealed record Entry(MediaId Id, PcmAudio Audio, long Bytes);
}
