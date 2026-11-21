namespace AudioLab.Web.Components;

/// <summary>Maps a renderer choice to the component that draws the lanes.</summary>
public static class LaneSurfaceCatalog
{
    /// <summary>The component type for a renderer.</summary>
    public static Type TypeFor(RendererKind renderer)
    {
        return renderer switch
        {
            RendererKind.Svg or RendererKind.SvgVirtual => typeof(SvgLaneSurface),
            RendererKind.Canvas2D => typeof(CanvasLaneSurface),
#if !NOSKIA
            RendererKind.SkiaGl or RendererKind.SkiaCpu => typeof(SkiaLaneSurface),
#endif
            _ => throw new NotSupportedException($"This build does not include the {renderer} candidate."),
        };
    }

    /// <summary>The parameters the component takes for a renderer.</summary>
    public static Dictionary<string, object?> ParametersFor(RendererKind renderer)
    {
        return renderer switch
        {
            RendererKind.Svg => new() { ["Virtualize"] = false },
            RendererKind.SvgVirtual => new() { ["Virtualize"] = true },
            RendererKind.SkiaGl => new() { ["Gpu"] = true },
            RendererKind.SkiaCpu => new() { ["Gpu"] = false },
            _ => [],
        };
    }
}
