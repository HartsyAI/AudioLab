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

    /// <summary>The element id of one step of a lane's grid.</summary>
    public static string CellId(BeatLaneId lane, int step) => $"drum-step-{lane.Value}-{step}";

    /// <summary>The accessible name of one step, which names its lane; whether it is on is the button's pressed state.</summary>
    public static string StepName(string lane, int step) => $"Step {step + 1} of {lane}";

    /// <summary>The accessible name of a lane's gain field.</summary>
    public static string GainName(string lane) => $"Gain of {lane}";

    /// <summary>The accessible name of a lane's Set gain button.</summary>
    public static string SetGainName(string lane) => $"Set gain of {lane}";

    private static string Word(MoveDirection direction) => direction == MoveDirection.Up ? "earlier" : "later";
}
