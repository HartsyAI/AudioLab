using AudioLab.Core.Model;

namespace AudioLab.Server.Media;

/// <summary>Decides whether a user may store one more file; the hosted door replaces the default with accounts and plans.</summary>
public interface IMediaQuota
{
    /// <summary>Why <paramref name="user"/> may not store <paramref name="incomingBytes"/> more, or null when they may.</summary>
    /// <remarks>The usage counts what the user has stored and what their other uploads have received so far.</remarks>
    /// <param name="user">The user the door resolved.</param>
    /// <param name="usage">What the user already has.</param>
    /// <param name="incomingBytes">The size of the file, or 0 when it is not known yet.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    public ValueTask<string?> RefusalAsync(UserId user, MediaUsage usage, long incomingBytes, CancellationToken cancellationToken);

    /// <summary>What <paramref name="user"/> may keep, to tell a client before it uploads; the limits <see cref="RefusalAsync"/> enforces.</summary>
    /// <param name="user">The user the door resolved.</param>
    /// <param name="cancellationToken">Cancels the question.</param>
    /// <remarks>
    /// The default says there is no fixed limit, for a quota whose rule is not two numbers (a plan, a share of a pool): the remaining
    /// numbers are then as large as they can be and <see cref="RefusalAsync"/> is still what refuses an upload. Such a quota overrides this
    /// with the numbers that fit it best.
    /// </remarks>
    public ValueTask<MediaQuotaLimits> LimitsAsync(UserId user, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new MediaQuotaLimits(int.MaxValue, long.MaxValue));
    }
}
