using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a marker.</summary>
public sealed record RemoveMarker : Command
{
    /// <summary>The marker to remove.</summary>
    [Description("The marker to remove.")]
    public required MarkerId Marker { get; init; }
}
