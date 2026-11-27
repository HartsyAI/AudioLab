namespace AudioLab.Web.Media;

/// <summary>A file the user chose to add, which can be opened more than once (once to check its header, once to send it).</summary>
public interface IUploadSource
{
    /// <summary>The file's name, which becomes the media file's label.</summary>
    public string Name { get; }

    /// <summary>The size of the file in bytes.</summary>
    public long Length { get; }

    /// <summary>Opens the file from its start; the caller disposes the stream.</summary>
    public Stream Open();

    /// <summary>The content type the file is sent as, which the server reads it by; a WAV file unless the source says otherwise.</summary>
    public string ContentType => MediaTypes.Wav;
}
