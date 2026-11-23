namespace AudioLab.Web.Session.Roll;

/// <summary>What a drag on the notes does.</summary>
public enum NoteDragKind
{
    /// <summary>Moves the selected notes in time and pitch.</summary>
    Move,

    /// <summary>Gives the selected notes a new length.</summary>
    Resize,

    /// <summary>Draws one note.</summary>
    Draw,

    /// <summary>Removes the notes the pointer crosses.</summary>
    Erase,

    /// <summary>Draws a rectangle and selects the notes in it.</summary>
    Marquee,
}
