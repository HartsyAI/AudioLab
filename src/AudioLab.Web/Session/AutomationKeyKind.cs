namespace AudioLab.Web.Session;

/// <summary>What a key asks an automation lane to do.</summary>
public enum AutomationKeyKind
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>Select the point at the index, or none when the index is null.</summary>
    Select,

    /// <summary>Add a point after the point at the index.</summary>
    Add,

    /// <summary>Remove the point at the index.</summary>
    Delete,

    /// <summary>Move the point at the index one step in a direction.</summary>
    Nudge,
}
