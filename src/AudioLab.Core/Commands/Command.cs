using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>An edit of one project: an immutable record that names its target and holds its fields, and is the wire format.</summary>
/// <remarks>A command never carries an id the server allocates: new ids come from the id service inside its handler.</remarks>
public abstract record Command
{
    /// <summary>The project the command edits; a command for any other open project is refused.</summary>
    public required ProjectId Project { get; init; }
}
