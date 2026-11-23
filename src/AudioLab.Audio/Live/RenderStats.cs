namespace AudioLab.Audio.Live;

/// <summary>How the engine's chunks have rendered since the last reading.</summary>
/// <param name="Chunks">How many chunks were rendered in all.</param>
/// <param name="Overruns">How many took longer to render than the audio in them lasts, in all.</param>
/// <param name="MaxMilliseconds">The longest render since the last reading.</param>
public readonly record struct RenderStats(long Chunks, long Overruns, double MaxMilliseconds);
