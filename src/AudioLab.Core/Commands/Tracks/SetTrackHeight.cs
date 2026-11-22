using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>Sets the height of a track's lane, in pixels at the default zoom.</summary>
public sealed record SetTrackHeight : Command
{
    /// <summary>The track to edit.</summary>
    [Description("The track to edit.")]
    public required TrackId Track { get; init; }

    /// <summary>The height, from 16 to 2000; the track menu offers 30, 44, 64, 100 and 160.</summary>
    [Description("The height, from 16 to 2000; the track menu offers 30, 44, 64, 100 and 160.")]
    [Range(TrackRules.MinHeight, TrackRules.MaxHeight)]
    public required double Height { get; init; }
}
