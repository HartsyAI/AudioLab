using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Api;

/// <summary>The body of a request to export a project as a WAV file or as a ZIP of stems; every member is optional.</summary>
/// <param name="Format">How the samples are stored; 24-bit PCM when not given.</param>
/// <param name="Dither">The dither for an integer format; triangular when not given.</param>
/// <param name="Seed">Seeds the dither, so the same project and options give the same file; 1 when not given.</param>
/// <param name="SampleRate">The rate to render at, in hertz: 44100, 48000, 88200 or 96000; 48000 when not given.</param>
/// <param name="Start">Where the export starts on the timeline, in seconds; 0 when not given.</param>
/// <param name="End">Where the content ends, in seconds; the end of the last audible clip when not given.</param>
/// <param name="Tail">Whether to run on past the end until effects have rung out; true when not given.</param>
/// <param name="TailSeconds">How long that run-on is, in seconds, instead of as long as the effects need; ignored without a tail.</param>
/// <param name="Tracks">The tracks to make stems of, for a stem export only; every track with sound and every fed bus when not given.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExportRequest(
    ExportFormat Format = ExportFormat.Pcm24,
    ExportDither Dither = ExportDither.Triangular,
    uint Seed = 1,
    int SampleRate = 48000,
    double Start = 0,
    double? End = null,
    bool Tail = true,
    double? TailSeconds = null,
    IReadOnlyList<TrackId>? Tracks = null);
