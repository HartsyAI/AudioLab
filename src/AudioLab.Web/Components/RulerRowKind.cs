namespace AudioLab.Web.Components;

/// <summary>The rows a ruler may have, top to bottom as the ruler stacks them.</summary>
public enum RulerRowKind
{
    /// <summary>The named blocks of the arrangement.</summary>
    Sections,

    /// <summary>Bar numbers and their ticks.</summary>
    Bars,

    /// <summary>Minutes and seconds.</summary>
    Time,

    /// <summary>The named points.</summary>
    Markers,
}
