namespace AudioLab.Web.Components;

/// <summary>The colour of the six-pixel dot of a status item.</summary>
public enum StatusDot
{
    /// <summary>Connected, or well.</summary>
    Go,

    /// <summary>Connecting again, not connected, failed, or something to know. Red is for recording alone (section 7.2).</summary>
    Caution,

    /// <summary>Another party, such as an agent.</summary>
    Agent,
}
