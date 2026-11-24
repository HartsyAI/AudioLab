namespace AudioLab.Web.Services;

/// <summary>How much of a file the page has handed to the browser to send.</summary>
/// <param name="BytesRead">Bytes taken from the file so far.</param>
/// <param name="TotalBytes">The size of the file.</param>
public readonly record struct UploadProgress(long BytesRead, long TotalBytes)
{
    /// <summary>The share read, from 0 to 1.</summary>
    public double Fraction => TotalBytes <= 0 ? 0 : Math.Clamp((double)BytesRead / TotalBytes, 0, 1);
}
