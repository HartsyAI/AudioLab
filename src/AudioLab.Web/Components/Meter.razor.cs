using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue stereo meter: two slots of hard steps, green to -12 dBFS, yellow to -3, red above. It is drawn, never transitioned, and idle until the engine sends levels.</summary>
public sealed partial class Meter
{
    /// <summary>The left level from 0 (silence) to 1 (full scale).</summary>
    [Parameter]
    public double Left { get; set; }

    /// <summary>The right level from 0 to 1.</summary>
    [Parameter]
    public double Right { get; set; }

    private string Description => Left <= 0 && Right <= 0 ? "Level: silent" : $"Level: {Percent(Left)} and {Percent(Right)} percent";

    private static string Cover(double level) => ((1 - Math.Clamp(level, 0, 1)) * 100).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Percent(double level) => Math.Round(Math.Clamp(level, 0, 1) * 100).ToString(CultureInfo.InvariantCulture);
}
