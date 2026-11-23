
namespace AudioLab.Core.Model;

/// <summary>What a client is told about one of its own media files. The owner is left out: a response never names a user.</summary>
/// <param name="Id">The id a clip, a drum lane or a sampler names.</param>
/// <param name="Name">The name the file was uploaded with.</param>
/// <param name="Kind">What the file holds.</param>
/// <param name="Format">The container.</param>
/// <param name="Encoding">How the samples are stored.</param>
/// <param name="Duration">The length of the whole file in seconds; a clip's duration must match it.</param>
/// <param name="SampleRate">Frames a second.</param>
/// <param name="Channels">Interleaved channels.</param>
/// <param name="Bytes">The size of the file.</param>
/// <param name="Sha256">The hash of the bytes, in lower-case hexadecimal.</param>
/// <param name="Created">When it was stored, in UTC.</param>
public sealed record MediaSummary(
    string Id,
    string Name,
    MediaKind Kind,
    MediaFormat Format,
    SampleEncoding Encoding,
    double Duration,
    int SampleRate,
    int Channels,
    long Bytes,
    string Sha256,
    DateTimeOffset Created)
{
    /// <summary>The summary of a record.</summary>
    public static MediaSummary Of(MediaRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new MediaSummary(
            record.Id.Value,
            record.Name,
            record.Kind,
            record.Format,
            record.Encoding,
            record.Duration,
            record.SampleRate,
            record.Channels,
            record.Bytes,
            record.Sha256,
            record.Created);
    }
}
