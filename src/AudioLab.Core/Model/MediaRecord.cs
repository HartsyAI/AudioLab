namespace AudioLab.Core.Model;

/// <summary>What the store knows about one media file; a project refers to the bytes by id.</summary>
public sealed record MediaRecord
{
    /// <summary>The media file's id.</summary>
    public required MediaId Id { get; init; }

    /// <summary>The user who owns the file. Every read and write of a file is scoped to it.</summary>
    public required UserId Owner { get; init; }

    /// <summary>The file's media type, such as audio/wav.</summary>
    public required string ContentType { get; init; }

    /// <summary>The file's size in bytes.</summary>
    public required long Length { get; init; }
}
