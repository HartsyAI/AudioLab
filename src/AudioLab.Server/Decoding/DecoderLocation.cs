using AudioLab.Server.Store;

namespace AudioLab.Server.Decoding;

/// <summary>Where the decoder lives: one folder inside the store root, one folder per version, and a temporary folder cleared at start.</summary>
public sealed class DecoderLocation
{
    private const string FolderName = "decoder";
    private const string TempName = "tmp";

    /// <summary>Creates the folders under the store root and removes what an interrupted install or decode left in the temporary folder.</summary>
    public DecoderLocation(StoreRoot root, ILogger<DecoderLocation> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(root);
        Folder = Path.Combine(root.Paths.Root, FolderName);
        TempFolder = Path.Combine(Folder, TempName);
        Directory.CreateDirectory(TempFolder);
        foreach (string leftover in Directory.EnumerateFiles(TempFolder))
        {
            try
            {
                File.Delete(leftover);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A file another process still holds, or one this process may not delete, is left for the next start; nothing reads it.
                DecoderLog.NotRemoved(logger, Path.GetFileName(leftover), exception.GetType().Name);
            }
        }
    }

    /// <summary>The decoder folder.</summary>
    public string Folder { get; }

    /// <summary>The folder for temporary archives, extracted binaries and decode inputs and outputs.</summary>
    public string TempFolder { get; }

    /// <summary>The path the binary of one version is installed at.</summary>
    public string Binary(string version, string binaryName)
    {
        ArgumentException.ThrowIfNullOrEmpty(binaryName);
        return Path.Combine(Folder, ValidVersion(version), binaryName);
    }

    // A version names a folder, so only digits and dots are a version here.
    private static string ValidVersion(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        return version.Length <= 32 && version.All(character => char.IsAsciiDigit(character) || character == '.')
            ? version
            : throw new ArgumentException("A decoder version is digits and dots only.", nameof(version));
    }
}
