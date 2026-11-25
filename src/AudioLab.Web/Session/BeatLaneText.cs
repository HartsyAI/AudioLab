using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Session;

/// <summary>The names and element ids of a drum lane's controls, so each one says which lane it changes and focus can return to it.</summary>
public static class BeatLaneText
{
    /// <summary>The element id of a lane's row.</summary>
    public static string RowId(BeatLaneId lane) => $"drum-lane-{lane.Value}";

    /// <summary>The element id of a lane's move button in one direction.</summary>
    public static string MoveId(BeatLaneId lane, MoveDirection direction) => $"drum-{Word(direction)}-{lane.Value}";

    /// <summary>The element id of a lane's Rename button.</summary>
    public static string RenameId(BeatLaneId lane) => $"drum-rename-{lane.Value}";

    /// <summary>The accessible name of a lane's move button, which names the lane.</summary>
    public static string MoveName(string lane, MoveDirection direction) => $"Move {lane} {(direction == MoveDirection.Up ? "earlier" : "later")}";

    /// <summary>The accessible name of a lane's Rename button.</summary>
    public static string RenameName(string lane) => $"Rename {lane}";

    /// <summary>The accessible name of a lane's Remove button.</summary>
    public static string RemoveName(string lane) => $"Remove {lane}";

    private static string Word(MoveDirection direction) => direction == MoveDirection.Up ? "earlier" : "later";
}
