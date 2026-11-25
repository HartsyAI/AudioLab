namespace AudioLab.Web.Components.Lyrics;

/// <summary>The file chooser in use: each pick hands over to a new chooser, however the handling of the pick ended.</summary>
/// <remarks>A chooser that has been picked from is replaced, so the same file can be picked again.</remarks>
public sealed class ChooserTurn
{
    /// <summary>The number of the chooser in use, which is its element's key.</summary>
    public int Number { get; private set; }

    /// <summary>Runs the handling of a pick, then moves on to a new chooser, even when the handling failed.</summary>
    /// <param name="handle">The handling of the picked file.</param>
    public async Task PickAsync(Func<Task> handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        try
        {
            await handle();
        }
        finally
        {
            Number++;
        }
    }
}
