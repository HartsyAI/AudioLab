using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

/// <summary>An id service whose ids are the same on every run, so expected values can be written down.</summary>
public sealed class SequentialIdService : IIdService
{
    private int _next;

    public TId Next<TId>()
        where TId : struct, IAllocatedId<TId>
    {
        _next++;
        return TId.From($"{TId.Prefix}_{_next}");
    }

    public NoteId NextNote(IReadOnlyCollection<MidiNote> existing)
    {
        int highest = 0;
        foreach (MidiNote note in existing)
        {
            highest = Math.Max(highest, note.Id.Value);
        }
        return new NoteId(highest).Next();
    }
}
