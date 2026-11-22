using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AudioLab.Core.Model;

namespace AudioLab.Server.Store;

/// <summary>The one place a store path is built. A name on disk is a hash of an id, so no id ever reaches a path.</summary>
public sealed partial class StorePaths
{
    /// <summary>The extension of a stored project or media record.</summary>
    public const string RecordExtension = ".json";

    /// <summary>The extension of the stored bytes of a media file.</summary>
    public const string DataExtension = ".wav";

    /// <summary>The extension of the stored peaks of a media file.</summary>
    public const string PeaksExtension = ".peaks";

    private const int NameHexLength = 32;

    private readonly string _root;

    /// <summary>Creates the paths under <paramref name="root"/>, an absolute path.</summary>
    public StorePaths(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        if (!Path.IsPathFullyQualified(root))
        {
            throw new ArgumentException("The store root must be an absolute path.", nameof(root));
        }
        _root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>The store's root folder.</summary>
    public string Root => _root;

    /// <summary>The file that keeps one process from sharing the store with another.</summary>
    public string LockFile => Inside(_root, "store.lock");

    /// <summary>The folder that holds every user's folder.</summary>
    public string UsersDirectory => Inside(_root, "users");

    /// <summary>The folder of one user's projects.</summary>
    public string ProjectsDirectory(UserId user) => Inside(UsersDirectory, UserName(user), "projects");

    /// <summary>The folder of one user's media records.</summary>
    public string MediaDirectory(UserId user) => Inside(UsersDirectory, UserName(user), "media");

    /// <summary>The file a project is kept in.</summary>
    public string ProjectFile(UserId user, ProjectId id) => Inside(ProjectsDirectory(user), ProjectFileName(id));

    /// <summary>The file a media record is kept in.</summary>
    public string MediaRecordFile(UserId user, MediaId id) => Inside(MediaDirectory(user), MediaFileName(id));

    /// <summary>The file a media file's bytes are kept in, beside its record.</summary>
    public string MediaDataFile(UserId user, MediaId id) => Inside(MediaDirectory(user), MediaDataFileName(id));

    /// <summary>The file a media file's peaks are kept in, beside its record.</summary>
    public string MediaPeaksFile(UserId user, MediaId id) => Inside(MediaDirectory(user), MediaPeaksFileName(id));

    /// <summary>A new temporary file beside the media file's peaks, which they are written to before it is renamed into place.</summary>
    public string MediaPeaksTempFile(UserId user, MediaId id) => $"{MediaPeaksFile(user, id)}.{Guid.NewGuid():N}.tmp";

    /// <summary>A new temporary file beside the media file's bytes, which the upload is written to before it is renamed into place.</summary>
    public string MediaTempFile(UserId user, MediaId id) => $"{MediaDataFile(user, id)}.{Guid.NewGuid():N}.tmp";

    /// <summary>The name of the file a project is kept in, without its folder.</summary>
    public static string ProjectFileName(ProjectId id) => Name('p', id.Value) + RecordExtension;

    /// <summary>The name of the file a media record is kept in, without its folder.</summary>
    public static string MediaFileName(MediaId id) => Name('m', id.Value) + RecordExtension;

    /// <summary>The name of the file a media file's bytes are kept in, without its folder.</summary>
    public static string MediaDataFileName(MediaId id) => Name('m', id.Value) + DataExtension;

    /// <summary>The name of the file a media file's peaks are kept in, without its folder.</summary>
    public static string MediaPeaksFileName(MediaId id) => Name('m', id.Value) + PeaksExtension;

    /// <summary>Whether a file name is one the store could have written for a media file's bytes or peaks.</summary>
    public static bool IsDataFileName(string fileName) => DataNamePattern().IsMatch(fileName);

    /// <summary>The name of the media record a bytes or peaks file belongs to, or null when the name is not one.</summary>
    public static string? RecordNameOfData(string fileName)
    {
        return IsDataFileName(fileName) ? fileName[..fileName.IndexOf('.', StringComparison.Ordinal)] + RecordExtension : null;
    }

    /// <summary>Whether a file name is one the store could have written for a project or media record.</summary>
    public static bool IsRecordFileName(string fileName) => RecordNamePattern().IsMatch(fileName);

    /// <summary>Whether a file name is a temporary file an interrupted write left behind.</summary>
    public static bool IsLeftoverTemp(string fileName) => TempNamePattern().IsMatch(fileName);

    private static string UserName(UserId user) => Name('u', user.Value);

    private static string Name(char kind, string id)
    {
        IdRules.Require(id);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"audiolab-store-v1:{kind}:{id}"));
        return kind + Convert.ToHexStringLower(hash)[..NameHexLength];
    }

    private string Inside(string folder, params string[] names)
    {
        string path = Path.GetFullPath(Path.Combine([folder, .. names]));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A store path resolved outside the store root.");
        }
        return path;
    }

    [GeneratedRegex(@"\A[pm][0-9a-f]{32}\.json\z", RegexOptions.CultureInvariant)]
    private static partial Regex RecordNamePattern();

    [GeneratedRegex(@"\A[pm][0-9a-f]{32}\.(json|wav|peaks)\.[0-9a-f]{32}\.tmp\z", RegexOptions.CultureInvariant)]
    private static partial Regex TempNamePattern();

    [GeneratedRegex(@"\Am[0-9a-f]{32}\.(wav|peaks)\z", RegexOptions.CultureInvariant)]
    private static partial Regex DataNamePattern();
}
