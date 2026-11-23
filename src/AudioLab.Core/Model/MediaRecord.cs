namespace AudioLab.Core.Model;

/// <summary>What the store knows about one media file; a project refers to the bytes by id.</summary>
/// <remarks>Everything but the name was measured from the bytes when they were uploaded, never taken from the client.</remarks>
public sealed record MediaRecord
{
    /// <summary>The media file's id.</summary>
    public required MediaId Id { get; init; }

    /// <summary>The user who owns the file. Every read and write of a file is scoped to it.</summary>
    public required UserId Owner { get; init; }

    /// <summary>The name the file had when it was uploaded; a label, never a path.</summary>
    public required string Name { get; init; }

    /// <summary>What the file holds.</summary>
    public required MediaKind Kind { get; init; }

    /// <summary>The container the file is stored in.</summary>
    public required MediaFormat Format { get; init; }

    /// <summary>How the samples are stored.</summary>
    public required SampleEncoding Encoding { get; init; }

    /// <summary>The length of the whole file in seconds: its frames divided by its sample rate. A clip's duration must match it.</summary>
    public required double Duration { get; init; }

    /// <summary>Frames a second.</summary>
    public required int SampleRate { get; init; }

    /// <summary>Interleaved channels in each frame.</summary>
    public required int Channels { get; init; }

    /// <summary>The file's size in bytes.</summary>
    public required long Bytes { get; init; }

    /// <summary>The SHA-256 of the file's bytes, 64 lower-case hexadecimal digits.</summary>
    public required string Sha256 { get; init; }

    /// <summary>When the file was stored, in UTC.</summary>
    public required DateTimeOffset Created { get; init; }
}
