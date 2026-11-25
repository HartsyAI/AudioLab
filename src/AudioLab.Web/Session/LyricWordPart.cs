namespace AudioLab.Web.Session;

/// <summary>Which part of a word a drag or a key moves.</summary>
public enum LyricWordPart
{
    /// <summary>The whole word: its start and its end together, so its length stays.</summary>
    Body,

    /// <summary>The start of the word.</summary>
    Start,

    /// <summary>The end of the word.</summary>
    End,
}
