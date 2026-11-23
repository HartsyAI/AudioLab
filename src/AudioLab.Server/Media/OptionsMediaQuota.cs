using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>The quota of <see cref="MediaOptions"/>: the same limits for every user.</summary>
public sealed class OptionsMediaQuota(MediaOptions options) : IMediaQuota
{
    /// <inheritdoc />
    public ValueTask<string?> RefusalAsync(UserId user, MediaUsage usage, long incomingBytes, CancellationToken cancellationToken)
    {
        string? refusal = null;
        if (usage.Files >= options.MaxFilesPerUser)
        {
            refusal = $"You already have {options.MaxFilesPerUser} media files, the most allowed; delete one first.";
        }
        else if (usage.Bytes + incomingBytes > options.MaxBytesPerUser)
        {
            refusal = $"This file would take your media past {options.MaxBytesPerUser} bytes, the most allowed; delete some first.";
        }
        return ValueTask.FromResult(refusal);
    }
}
