namespace AudioLab.Web.Session.Roll;

/// <summary>What a press on the notes does.</summary>
public enum RollTool
{
    /// <summary>Selects notes, moves them and resizes them.</summary>
    Select,

    /// <summary>Draws a note where the pointer goes down; a press on a note acts as the select tool does.</summary>
    Draw,

    /// <summary>Removes the notes the pointer crosses.</summary>
    Erase,
}
