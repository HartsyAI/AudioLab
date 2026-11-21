namespace AudioLab.Web.Components;

/// <summary>The four toast variants of the catalogue.</summary>
public enum AlToastKind
{
    /// <summary>Something the user should know.</summary>
    Info,

    /// <summary>Something finished.</summary>
    Success,

    /// <summary>Something needs attention.</summary>
    Warning,

    /// <summary>Something failed; stays until dismissed.</summary>
    Error,
}
