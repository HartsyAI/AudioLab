namespace AudioLab.Web.Session;

/// <summary>Whether undo and redo wait: while a clip, a word or the roll is being dragged, a step would be taken back under the drag.</summary>
public static class HistoryGate
{
    /// <summary>Whether a press of undo or redo waits for the drag that is under way to end.</summary>
    /// <param name="clipDragging">Whether a clip is being dragged.</param>
    /// <param name="rollBusy">Whether an edit of the piano roll is on its way.</param>
    /// <param name="wordDragging">Whether a word of the lyrics is being dragged.</param>
    public static bool Waits(bool clipDragging, bool rollBusy, bool wordDragging) => clipDragging || rollBusy || wordDragging;
}
