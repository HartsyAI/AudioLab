using AudioLab.Core.Model;

namespace AudioLab.Web.Media;

/// <summary>Where a media file is to become a clip: the file, the audio track and the start in seconds, as <c>clip_add</c> takes them.</summary>
/// <param name="File">The file; its duration is sent verbatim, since the server holds a clip to the file's own length (D22).</param>
/// <param name="Track">The audio track the clip goes on.</param>
/// <param name="Start">Where it starts on the timeline, in seconds.</param>
public readonly record struct MediaPlacement(MediaSummary File, TrackId Track, double Start);
