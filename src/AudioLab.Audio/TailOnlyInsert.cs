namespace AudioLab.Audio;

/// <summary>An insert that does nothing and only says how long its effect rings, for sizing a render without building the effect.</summary>
internal sealed class TailOnlyInsert(double tailSeconds, int latencyFrames, bool tailIsCut) : IInsert
{
    public double TailSeconds => tailSeconds;

    public int LatencyFrames => latencyFrames;

    public bool TailIsCut => tailIsCut;

    public void Reset()
    {
    }

    public void Process(Span<float> left, Span<float> right, long startFrame)
    {
    }
}
