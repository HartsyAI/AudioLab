using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes the stem link of an audio clip.</summary>
public sealed record ClearClipStem : Command
{
    /// <summary>The audio clip to edit.</summary>
    [Description("The audio clip to edit.")]
    public required ClipId Clip { get; init; }
}
