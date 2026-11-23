using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>Says before a project drops out whether the engine in the page, as it was built, can be expected to play it (D60).</summary>
/// <remarks>
/// Measured interpreted on one machine: an EQ and a compressor on 16 tracks used 40% of a core and a reverb, a delay and a saturator on
/// a quarter of the tracks each took the mix to 383%; ahead of time that mix took 40%. So any of the three heavy effects is advice enough.
/// </remarks>
public static class EngineLoadAdvice
{
    private const int ManyLightEffects = 16;

    /// <summary>A sentence to show when the interpreted engine is unlikely to keep up with the project, or null.</summary>
    public static string? Warning(ProjectDocument document, bool compiledAhead)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (compiledAhead)
        {
            return null;
        }
        Effect[] effects = [.. document.Tracks.SelectMany(track => track.Effects).Where(effect => effect.Enabled)];
        int heavy = effects.Count(effect => effect is ReverbEffect or DelayEffect or SaturationEffect);
        int light = effects.Length - heavy;
        if (heavy == 0 && light <= ManyLightEffects)
        {
            return null;
        }
        return "This build of the audio engine is interpreted, and a project with "
            + (heavy > 0 ? "reverb, delay or saturation" : "this many effects")
            + " will probably drop out. Publish the server with the ahead-of-time engine (docs/engine-aot.md).";
    }
}
