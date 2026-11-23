namespace AudioLab.Audio;

/// <summary>One stretch of an audio clip that is heard, in timeline frames, with everything the render loop needs to play it.</summary>
internal readonly struct PlannedClip
{
    private readonly long _clipStart;
    private readonly long _clipEnd;
    private readonly long _spanStart;
    private readonly long _spanEnd;
    private readonly long _sourceOffset;
    private readonly long _fadeIn;
    private readonly long _fadeOut;
    private readonly long _seam;
    private readonly float _gain;
    private readonly bool _seamIn;
    private readonly bool _seamOut;

    public PlannedClip(
        PcmAudio audio,
        (long Start, long End) clip,
        (long Start, long End) span,
        long sourceOffset,
        (long In, long Out) fades,
        (bool In, bool Out, long Frames) seams,
        float gain)
    {
        Audio = audio;
        _clipStart = clip.Start;
        _clipEnd = clip.End;
        _spanStart = span.Start;
        _spanEnd = span.End;
        _sourceOffset = sourceOffset;
        _fadeIn = fades.In;
        _fadeOut = fades.Out;
        _seamIn = seams.In;
        _seamOut = seams.Out;
        _seam = seams.Frames;
        _gain = gain;
    }

    public PcmAudio Audio { get; }

    /// <summary>The first timeline frame this stretch is heard on.</summary>
    public long SpanStart => _spanStart;

    /// <summary>The frame after the last one this stretch is heard on.</summary>
    public long SpanEnd => _spanEnd;

    /// <summary>Adds this stretch's part of a block to the outputs; <paramref name="blockStart"/> is the block's first timeline frame.</summary>
    public void MixInto(long blockStart, Span<float> left, Span<float> right)
    {
        // Frames past either end of the media are silent, whatever length the clip claims for the file.
        long length = Audio.Frames;
        long from = Math.Max(Math.Max(blockStart, _spanStart), _clipStart - _sourceOffset);
        long to = Math.Min(Math.Min(blockStart + left.Length, _spanEnd), _clipStart - _sourceOffset + length);
        if (to <= from)
        {
            return;
        }
        ReadOnlySpan<float> sourceLeft = Audio.Channel(0);
        ReadOnlySpan<float> sourceRight = Audio.ChannelCount > 1 ? Audio.Channel(1) : sourceLeft;
        for (long frame = from; frame < to; frame++)
        {
            int source = (int)(_sourceOffset + (frame - _clipStart));
            int at = (int)(frame - blockStart);
            float gain = GainAt(frame);
            left[at] += sourceLeft[source] * gain;
            right[at] += sourceRight[source] * gain;
        }
    }

    // The clip gain times its fades and the seam ramps, all worked out from the absolute frame so no block size changes them.
    private float GainAt(long frame)
    {
        float gain = _gain;
        long since = frame - _clipStart;
        long until = _clipEnd - frame;
        if (since < _fadeIn)
        {
            gain *= (float)since / _fadeIn;
        }
        if (until < _fadeOut)
        {
            gain *= (float)until / _fadeOut;
        }
        if (_seamIn && frame - _spanStart < _seam)
        {
            gain *= (float)(frame - _spanStart) / _seam;
        }
        if (_seamOut && _spanEnd - frame < _seam)
        {
            gain *= (float)(_spanEnd - frame) / _seam;
        }
        return gain;
    }
}
