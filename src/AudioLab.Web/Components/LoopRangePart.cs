namespace AudioLab.Web.Components;

/// <summary>Which part of the loop range a <see cref="LoopRange"/> draws.</summary>
public enum LoopRangePart
{
    /// <summary>The accent band between two accent edges, in the bars row of the ruler.</summary>
    Ruler,

    /// <summary>The wash over the lanes.</summary>
    Lanes,
}
