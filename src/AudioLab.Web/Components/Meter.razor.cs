using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>A stereo meter: the colour scale with the part above the level covered by the well.</summary>
public sealed partial class Meter : ComponentBase
{
    /// <summary>Left channel level, percent of full scale.</summary>
    [Parameter]
    public int Left { get; set; }

    /// <summary>Right channel level, percent of full scale.</summary>
    [Parameter]
    public int Right { get; set; }
}
