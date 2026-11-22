namespace AudioLab.Server.Store;

/// <summary>The points of an atomic write at which a crash could happen; a test observes the disk at each.</summary>
public enum AtomicWriteStage
{
    /// <summary>The temporary file exists and is empty.</summary>
    TempCreated,

    /// <summary>Half of the content is in the temporary file.</summary>
    PartlyWritten,

    /// <summary>All the content is in the temporary file and has been flushed to disk, which is still open.</summary>
    Flushed,

    /// <summary>The temporary file is closed and complete; the destination still holds its old content.</summary>
    BeforeRename,

    /// <summary>The temporary file has replaced the destination.</summary>
    Renamed,
}
