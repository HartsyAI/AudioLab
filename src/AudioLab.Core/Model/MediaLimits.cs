using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>What a user may still do with media: the limits that apply to them and what they have used, to check before an upload.</summary>
/// <remarks>The owner is left out: a response never names a user. The numbers are advice: another upload can finish first.</remarks>
/// <param name="MaxUploadBytes">The largest file one upload may carry.</param>
/// <param name="MaxFiles">The most files the user may keep.</param>
/// <param name="MaxBytes">The most bytes of media the user may keep.</param>
/// <param name="MaxConcurrentUploads">The most uploads the user may have running at once.</param>
/// <param name="Files">How many files the user has, counting what their running uploads have been admitted for.</param>
/// <param name="Bytes">How many bytes the user has, counting what their running uploads have received so far.</param>
/// <param name="FilesRemaining">How many more files fit, never below 0.</param>
/// <param name="BytesRemaining">How many more bytes fit, never below 0.</param>
/// <param name="UploadsRunning">How many uploads the user has running now.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MediaLimits(
    long MaxUploadBytes,
    int MaxFiles,
    long MaxBytes,
    int MaxConcurrentUploads,
    int Files,
    long Bytes,
    int FilesRemaining,
    long BytesRemaining,
    int UploadsRunning);
