namespace AudioLab.Server.Store;

/// <summary>The user already has as many projects as the store allows.</summary>
public sealed class QuotaExceededException : Exception
{
    /// <summary>Creates the exception for a limit of <paramref name="limit"/> projects.</summary>
    public QuotaExceededException(int limit)
        : base($"You already have {limit} projects, the most allowed; delete one first.")
    {
    }
}
