using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Drops the word times of a clip's lyrics and keeps the text.</summary>
public sealed record ClearLyricsTiming : Command
{
    /// <summary>The audio clip whose lyrics are edited.</summary>
    [Description("The audio clip whose lyrics are edited.")]
    public required ClipId Clip { get; init; }
}
