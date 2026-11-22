using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Gives a marker a colour, or the default colour when none is given.</summary>
public sealed record SetMarkerColor : Command
{
    /// <summary>The marker to colour.</summary>
    public required MarkerId Marker { get; init; }

    /// <summary>A colour as #rgb, #rrggbb or #rrggbbaa; absent for the default.</summary>
    public string? Color { get; init; }
}
