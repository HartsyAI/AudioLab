namespace AudioLab.Web.Session;

/// <summary>Whether undo and redo wait: while a clip, a word, the roll or a paint drag is under way, a step would be taken back under the drag.</summary>
public static class HistoryGate
{
    /// <summary>Whether a press of undo or redo waits for the drag that is under way to end.</summary>
    /// <param name="clipDragging">Whether a clip is being dragged.</param>
    /// <param name="rollBusy">Whether an edit of the piano roll is on its way.</param>
    /// <param name="wordDragging">Whether a word of the lyrics is being dragged.</param>
    /// <param name="paintDragging">Whether a paint drag over the drum steps has not finished.</param>
    public static bool Waits(bool clipDragging, bool rollBusy, bool wordDragging, bool paintDragging = false)
        => clipDragging || rollBusy || wordDragging || paintDragging;
}
