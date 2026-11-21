namespace AudioLab.Web.Rendering;

/// <summary>Facts about the Skia run that belong in the result: what failed to load and the surface it painted.</summary>
public sealed class SkiaDiagnostics
{
    /// <summary>Fonts Skia could not decode.</summary>
    public List<string> FontFailures { get; } = [];

    /// <summary>Size of the surface the last paint received.</summary>
    public string LastSurface { get; set; } = string.Empty;
}
