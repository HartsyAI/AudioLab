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

    /// <inheritdoc />
    public NoteId NextNote(IReadOnlyCollection<MidiNote> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        int highest = 0;
        foreach (MidiNote note in existing)
        {
            highest = Math.Max(highest, note.Id.Value);
        }
        return new NoteId(highest).Next();
    }
}
