namespace AudioLab.Web.Components;

/// <summary>The colour of the six-pixel dot of a status item.</summary>
public enum StatusDot
{
    /// <summary>Connected, or well.</summary>
    Go,

    /// <summary>Connecting again, or something to know.</summary>
    Caution,

    /// <summary>Not connected, or failed.</summary>
    Record,

    /// <summary>Another party, such as an agent.</summary>
    Agent,
}
