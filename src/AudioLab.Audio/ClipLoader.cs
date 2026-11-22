using System.Diagnostics;

namespace AudioLab.Audio;

/// <summary>Fetches WAV clips over HTTP and decodes them for the engine.</summary>
public sealed class ClipLoader(HttpClient http)
{
    private readonly List<SampleClip> _clips = [];

    /// <summary>The clips loaded so far, in request order.</summary>
    public IReadOnlyList<SampleClip> Clips => _clips;

    /// <summary>Fetches the clips one after another, as the page would fetch a project's media.</summary>
    public async Task<ClipLoadResult> LoadAsync(Uri[] addresses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        long started = Stopwatch.GetTimestamp();
        long bytes = 0;
        long decoded = 0;
        double seconds = 0;
        foreach (Uri address in addresses)
        {
            byte[] file = await http.GetByteArrayAsync(address, cancellationToken);
            bytes += file.Length;
            SampleClip clip = WavReader.Read(file);
            decoded += clip.Samples.Length * sizeof(short);
            seconds += (double)clip.Frames / clip.SampleRate;
            _clips.Add(clip);
        }
        return new ClipLoadResult
        {
            Clips = addresses.Length,
            Bytes = bytes,
            Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            DecodedBytes = decoded,
            Seconds = seconds,
        };
    }
}
