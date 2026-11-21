namespace AudioLab.Web.Session;

/// <summary>One channel strip of the mixer: a track, a bus or the master.</summary>
public sealed class StripModel
{
    /// <summary>Strip name.</summary>
    public required string Name { get; init; }

    /// <summary>Colour token of the strip's dot.</summary>
    public required string ColourToken { get; init; }

    /// <summary>Width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Whether the strip is the master, which sits on the selected surface.</summary>
    public required bool Master { get; init; }

    /// <summary>Fader position, percent.</summary>
    public required int Fader { get; init; }

    /// <summary>Pan pointer angle in degrees.</summary>
    public required int Pan { get; init; }

    /// <summary>Pan readout.</summary>
    public required string PanText { get; init; }

    /// <summary>Volume readout in dB.</summary>
    public required string DbText { get; init; }

    /// <summary>Left meter level, percent.</summary>
    public required int MeterLeft { get; init; }

    /// <summary>Right meter level, percent.</summary>
    public required int MeterRight { get; init; }

    /// <summary>Whether the strip is muted.</summary>
    public required bool Muted { get; init; }

    /// <summary>Effect slot names; an empty name is an empty slot.</summary>
    public required string[] Slots { get; init; }
}
