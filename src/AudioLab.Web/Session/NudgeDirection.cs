namespace AudioLab.Web.Session;

/// <summary>A direction a key moves a point in.</summary>
public enum NudgeDirection
{
    /// <summary>Towards the start of the timeline.</summary>
    Earlier,

    /// <summary>Towards the end of the timeline.</summary>
    Later,

    /// <summary>Up the lane.</summary>
    Higher,

    /// <summary>Down the lane.</summary>
    Lower,
}
