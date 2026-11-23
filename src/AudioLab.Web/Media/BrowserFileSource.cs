using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Media;

/// <summary>A file chosen in the browser, as an upload source.</summary>
public sealed class BrowserFileSource(IBrowserFile file) : IUploadSource
{
    /// <inheritdoc />
    public string Name => file.Name;

    /// <inheritdoc />
    public long Length => file.Size;

    /// <inheritdoc />
    public Stream Open() => file.OpenReadStream(Math.Max(1, file.Size));
}
