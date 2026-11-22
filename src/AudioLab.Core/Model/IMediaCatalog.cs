namespace AudioLab.Core.Model;

/// <summary>What a project may ask about the media files of one user; Core holds no records, so the server provides this.</summary>
public interface IMediaCatalog
{
    /// <summary>The record of a file that exists and is owned by <paramref name="owner"/>; null when there is none.</summary>
    /// <remarks>
    /// A missing file and another user's file both give null, so a caller cannot tell them apart (D12).
    /// It answers from an index in memory; the first question about a user may read their folder, so a caller that cannot wait
    /// asks the server to prepare the user first. While the folder cannot be read it finds nothing.
    /// </remarks>
    public MediaRecord? Find(UserId owner, MediaId id);

    /// <summary>The record of a file that exists and is owned by <paramref name="owner"/>, even while its delete is pending.</summary>
    /// <remarks>For a document that already names the file: a delete that a project uses is refused, so it is no reason to refuse it.</remarks>
    public MediaRecord? FindIncludingPendingDelete(UserId owner, MediaId id);

    /// <summary>Whether a delete of the file has begun and may still be refused, so a document must not take it up now.</summary>
    public bool IsDeleting(UserId owner, MediaId id);
}
