namespace AudioLab.Core.Model;

/// <summary>The production id service: a prefix and a time-ordered random value, unique across restarts.</summary>
public sealed class IdService : IIdService
{
    /// <inheritdoc />
    public TId Next<TId>()
        where TId : struct, IAllocatedId<TId>
    {
        return TId.From($"{TId.Prefix}_{Guid.CreateVersion7():N}");
    }
}
