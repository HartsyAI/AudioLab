using System.Text.Json.Serialization;

namespace AudioLab.Core.Playback;

/// <summary>What the worker that hosts the engine reports to the page: where it is and how it is doing.</summary>
/// <param name="State">Whether it is playing.</param>
/// <param name="Position">The timeline frame the engine renders next, which is ahead of what is heard by the audio the worklet holds.</param>
/// <param name="SampleRate">Frames a second.</param>
/// <param name="Revision">The revision of the document the plan in use was made from, or -1 before the first plan.</param>
/// <param name="Loading">How many clips are being fetched or prepared for the newest document.</param>
/// <param name="Problem">Why the newest document could not be played (a clip that cannot be fetched), or null.</param>
/// <param name="Chunks">How many chunks have been rendered.</param>
/// <param name="Overruns">How many chunks took longer to render than the audio they hold lasts.</param>
/// <param name="MaxRenderMs">The longest render of a chunk since the last report, in milliseconds.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HostStatus(
    TransportState State,
    long Position,
    int SampleRate,
    long Revision,
    int Loading,
    string? Problem,
    long Chunks,
    long Overruns,
    double MaxRenderMs);
