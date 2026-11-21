namespace AudioLab.Core.Model;

/// <summary>Allocates every id in a project. Only the server holds one; clients never mint ids.</summary>
public interface IIdService
{
    /// <summary>Allocates a new id that has never been used before.</summary>
    public TId Next<TId>()
        where TId : struct, IAllocatedId<TId>;

    /// <summary>The id for a new note in a clip that already holds <paramref name="existing"/>.</summary>
    public NoteId NextNote(IReadOnlyCollection<MidiNote> existing);
}
