using System.ComponentModel;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Puts a clip over or under the other clips of its track; the clip on top is the take that plays.</summary>
public sealed record ReorderClip : Command
{
    /// <summary>The clip to move.</summary>
    [Description("The clip to move.")]
    public required ClipId Clip { get; init; }

    /// <summary>front puts it over every other clip, back under every other clip.</summary>
    [Description("front puts it over every other clip, back under every other clip.")]
    public required ClipLayer Layer { get; init; }
}
