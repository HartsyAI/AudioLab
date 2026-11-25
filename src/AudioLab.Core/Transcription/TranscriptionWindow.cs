namespace AudioLab.Core.Transcription;

/// <summary>One window of a media file: where the model is given audio, and the part of the file whose segments it owns (D91).</summary>
/// <param name="Index">The window's place in the file, from 0.</param>
/// <param name="StartSeconds">Where the window's audio starts, in seconds of the media.</param>
/// <param name="LengthSeconds">How much audio the window holds, at most the window length.</param>
/// <param name="OwnedEndSeconds">Where the owned range ends; the last window owns through the end of the media.</param>
/// <param name="IsLast">Whether this is the last window of the media.</param>
public readonly record struct TranscriptionWindow(
    int Index,
    double StartSeconds,
    double LengthSeconds,
    double OwnedEndSeconds,
    bool IsLast);
