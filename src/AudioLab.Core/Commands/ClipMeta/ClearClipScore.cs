using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes the score record of an audio clip.</summary>
public sealed record ClearClipScore : Command
{
    /// <summary>The audio clip to edit.</summary>
    [Description("The audio clip to edit.")]
    public required ClipId Clip { get; init; }
}
