using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>Decides whether a user may store one more file; the hosted door replaces the default with accounts and plans.</summary>
public interface IMediaQuota
{
    /// <summary>Why <paramref name="user"/> may not store <paramref name="incomingBytes"/> more, or null when they may.</summary>
    /// <param name="user">The user the door resolved.</param>
    /// <param name="usage">What the user already has.</param>
    /// <param name="incomingBytes">The size of the file, or 0 when it is not known yet.</param>
    public string? Refusal(UserId user, MediaUsage usage, long incomingBytes);
}
