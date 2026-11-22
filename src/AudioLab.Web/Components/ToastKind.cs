namespace AudioLab.Web.Components;

/// <summary>The four toast variants of the catalogue.</summary>
public enum ToastKind
{
    /// <summary>Something the user should know.</summary>
    Info,

    /// <summary>Something finished.</summary>
    Success,

    /// <summary>Something needs attention.</summary>
    Warning,

    /// <summary>Something failed; it stays until dismissed.</summary>
    Error,
}
