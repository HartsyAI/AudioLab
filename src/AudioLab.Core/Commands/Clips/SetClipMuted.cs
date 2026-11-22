using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Mutes or unmutes an audio or MIDI clip.</summary>
public sealed record SetClipMuted : Command
{
    /// <summary>The clip to edit.</summary>
    [Description("The clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>True to silence the clip.</summary>
    [Description("True to silence the clip.")]
    public required bool Muted { get; init; }
}
