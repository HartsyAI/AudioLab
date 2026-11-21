using System.Globalization;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The ruler: sections above, bars below with the loop band and the playhead's cap.</summary>
public sealed partial class RulerRow : ComponentBase
{
    /// <summary>The session shown.</summary>
    [Parameter]
    [EditorRequired]
    public SessionModel Session { get; set; } = null!;

    /// <summary>Width of one bar in CSS pixels.</summary>
    [Parameter]
    public double BarWidth { get; set; }

    private IReadOnlyList<SectionModel> Sections => Session.Sections;

    private int BarCount => Session.Bars;

    private double Width => Session.Bars * BarWidth;

    private double LoopLeft => Session.LoopStartBar * BarWidth;

    private double LoopWidth => Session.LoopBars * BarWidth;

    private double HeadCapLeft => Math.Round(Session.PlayheadBar * BarWidth) - 5;

    private bool ShowLabel(int index) => BarWidth >= 40 || index % 4 == 0;

    private string BarClass(int index) => index % 4 == 0 ? "bar strong" : "bar";

    private string SectionStyle(SectionModel section)
    {
        string left = ((section.StartBar - 1) * BarWidth + 1).ToString(CultureInfo.InvariantCulture);
        string width = ((section.Bars * BarWidth) - 2).ToString(CultureInfo.InvariantCulture);
        return $"left: {left}px; width: {width}px; background: color-mix(in srgb, var({section.ColourToken}) 22%, transparent)";
    }
}
