namespace AudioLab.Web.Session;

/// <summary>The linear congruential generator the design mock uses, so seeded content matches it.</summary>
public struct Lcg(uint seed)
{
    private uint _state = seed;

    /// <summary>Next value in [0, 1).</summary>
    public double Next()
    {
        _state = unchecked((_state * 1664525u) + 1013904223u);
        return _state / 4294967296.0;
    }
}
