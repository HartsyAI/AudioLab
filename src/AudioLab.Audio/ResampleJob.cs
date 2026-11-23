namespace AudioLab.Audio;

/// <summary>A conversion to another sample rate that runs in slices, so a caller that must stay responsive can pause between them.</summary>
internal sealed class ResampleJob
{
    private readonly PcmAudio _source;
    private readonly int _rate;
    private readonly double _ratio;
    private readonly int _reach;
    private readonly int _taps;
    private readonly float[]? _table;
    private readonly float[][]? _output;
    private readonly int _frames;
    private int _done;

    /// <summary>Starts a conversion of <paramref name="source"/> to <paramref name="rate"/>.</summary>
    public ResampleJob(PcmAudio source, int rate)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _rate = rate;
        if (source.SampleRate == rate)
        {
            return;
        }
        _ratio = (double)source.SampleRate / rate;
        (double cutoff, double extent, int reach) = Resampler.Shape(_ratio);
        _reach = reach;
        _taps = 2 * reach;
        _table = Resampler.BuildTable(cutoff, extent, reach, _taps);
        _frames = (int)Math.Round(source.Frames / _ratio);
        _output = new float[source.ChannelCount][];
        for (int channel = 0; channel < _output.Length; channel++)
        {
            _output[channel] = new float[_frames];
        }
    }

    /// <summary>Whether every frame has been converted.</summary>
    public bool IsDone => _output is null || _done >= _frames;

    /// <summary>The audio at the new rate: the source itself when it already was, and only meaningful once <see cref="IsDone"/>.</summary>
    public PcmAudio Result => _output is null ? _source : new PcmAudio(_rate, _output);

    /// <summary>Converts up to <paramref name="frames"/> more output frames of every channel.</summary>
    public void Run(int frames)
    {
        if (_output is null || _table is null)
        {
            return;
        }
        int to = (int)Math.Min(_frames, (long)_done + frames);
        for (int channel = 0; channel < _output.Length; channel++)
        {
            Resampler.Convert(_source.Channel(channel), _output[channel], _done, to, _ratio, _reach, _taps, _table);
        }
        _done = to;
    }
}
