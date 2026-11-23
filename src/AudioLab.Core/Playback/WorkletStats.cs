using System.Text.Json.Serialization;

namespace AudioLab.Core.Playback;

/// <summary>What the audio worklet counts on the audio thread and reports to the page.</summary>
/// <param name="Position">The timeline frame of the next frame to be played, from the position each chunk carries.</param>
/// <param name="Fill">Frames waiting in the ring.</param>
/// <param name="MinFill">The fewest frames the ring held since the first fill.</param>
/// <param name="Events">Dropout events: runs of quanta that found less than a quantum of audio in the ring.</param>
/// <param name="Missed">Frames of silence played in place of audio.</param>
/// <param name="Primed">Whether the ring has filled once; nothing is counted before.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkletStats(double Position, int Fill, int MinFill, long Events, long Missed, bool Primed);
