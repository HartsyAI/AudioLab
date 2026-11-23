namespace AudioLab.Server.Render;

/// <summary>Why an export was refused; <see cref="ExportRefusals.Code"/> is the reason a client reads.</summary>
public enum ExportRefusal
{
    /// <summary>A request member is not one an export can have.</summary>
    BadOptions,

    /// <summary>The user or the server is already running as many exports as are allowed.</summary>
    Busy,

    /// <summary>A clip or an instrument names a file of the project that is not there.</summary>
    MissingMedia,

    /// <summary>The export would be longer or larger than the limits allow, or than its format holds.</summary>
    TooLarge,

    /// <summary>The export ran past the longest time one may take.</summary>
    TimedOut,

    /// <summary>The project has no sound in the range asked for, or no track to make a stem of.</summary>
    Empty,
}
