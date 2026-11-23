using AudioLab.Core.Model;

namespace AudioLab.Web.Services;

/// <summary>The sink the page has until the sound engine runs in it: nothing is heard, and it says why.</summary>
public sealed class UnavailableAudition : IAuditionSink
{
    /// <inheritdoc />
    public string? Unavailable => "Notes cannot be heard yet: the sound engine is not running in the page.";

    /// <inheritdoc />
    public Task NoteOnAsync(TrackId track, int pitch, int velocity) => Task.CompletedTask;

    /// <inheritdoc />
    public Task NoteOffAsync(TrackId track, int pitch) => Task.CompletedTask;
}
