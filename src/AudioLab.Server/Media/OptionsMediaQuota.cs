using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>The quota of <see cref="MediaOptions"/>: the same limits for every user.</summary>
public sealed class OptionsMediaQuota(MediaOptions options) : IMediaQuota
{
    /// <inheritdoc />
    public string? Refusal(UserId user, MediaUsage usage, long incomingBytes)
    {
        if (usage.Files >= options.MaxFilesPerUser)
        {
            return $"You already have {options.MaxFilesPerUser} media files, the most allowed; delete one first.";
        }
        if (usage.Bytes + incomingBytes > options.MaxBytesPerUser)
        {
            return $"This file would take your media past {options.MaxBytesPerUser} bytes, the most allowed; delete some first.";
        }
        return null;
    }
}
