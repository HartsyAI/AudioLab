namespace AudioLab.Server.Services;

/// <summary>The keys the server's <see cref="ConcurrencyGate"/>s are registered under.</summary>
public static class GateKeys
{
    /// <summary>The gate of exports, with the limits of <c>AudioLab:Render</c>.</summary>
    public const string Export = "export";

    /// <summary>The gate of MIDI imports, with the limits of <c>AudioLab:Midi</c>.</summary>
    public const string Midi = "midi";
}
