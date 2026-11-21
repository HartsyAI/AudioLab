using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

// The shape of the extension's version 3 project file, from serializeProject and serializeTrack in audio-daw.js and
// audio-daw-track.js. A field the extension might leave out is nullable; a field it never wrote is not here, so
// the strict V3 context refuses it.

/// <summary>A version 3 project.</summary>
internal sealed record V3Project
{
    public int? Version { get; init; }

    public string? ProjectName { get; init; }

    public double? Bpm { get; init; }

    public List<V3Tempo>? TempoMap { get; init; }

    public bool? MasterLimiterEnabled { get; init; }

    public List<int>? TimeSignature { get; init; }

    public double? MasterVolume { get; init; }

    public string? RulerMode { get; init; }

    public bool? SnapEnabled { get; init; }

    public string? EditMode { get; init; }

    public string? GridValue { get; init; }

    public List<V3Marker>? Markers { get; init; }

    public List<V3Section>? Sections { get; init; }

    public List<V3Group>? Groups { get; init; }

    public bool? Metronome { get; init; }

    public V3BeatPattern? BeatPattern { get; init; }

    public List<V3Track>? Tracks { get; init; }
}

internal sealed record V3Tempo
{
    public string? Id { get; init; }

    public double? T { get; init; }

    public double? Bpm { get; init; }
}

internal sealed record V3Marker
{
    public string? Id { get; init; }

    public double? Time { get; init; }

    public string? Name { get; init; }

    public string? Color { get; init; }
}

internal sealed record V3Section
{
    public string? Id { get; init; }

    public double? Start { get; init; }

    public double? End { get; init; }

    public string? Name { get; init; }

    public string? Color { get; init; }
}

internal sealed record V3Group
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public string? Color { get; init; }

    public bool? Collapsed { get; init; }

    public bool? Tint { get; init; }

    public bool? Muted { get; init; }

    public bool? Soloed { get; init; }

    public double? Volume { get; init; }
}

internal sealed record V3BeatPattern
{
    public int? Steps { get; init; }

    public double? Swing { get; init; }

    public List<V3BeatLane>? Lanes { get; init; }
}

internal sealed record V3BeatLane
{
    public string? Name { get; init; }

    public string? BlobKey { get; init; }

    public double? Gain { get; init; }

    public List<System.Text.Json.JsonElement>? Steps { get; init; }
}

internal sealed record V3Track
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public double? Volume { get; init; }

    public double? Pan { get; init; }

    public bool? Muted { get; init; }

    public bool? Soloed { get; init; }

    public string? GroupId { get; init; }

    public string? Icon { get; init; }

    public bool? Armed { get; init; }

    public bool? Monitor { get; init; }

    public V3Input? Input { get; init; }

    public int? TakeCount { get; init; }

    public string? Kind { get; init; }

    public V3Instrument? Instrument { get; init; }

    public List<V3MidiClip>? MidiClips { get; init; }

    public string? Color { get; init; }

    public double? Height { get; init; }

    public List<V3Fx>? Fx { get; init; }

    public V3Automation? Automation { get; init; }

    public bool? AutomationVisible { get; init; }

    public string? AutomationParam { get; init; }

    public string? AutomationMode { get; init; }

    public List<V3Send>? Sends { get; init; }

    public List<V3Clip>? Clips { get; init; }
}

internal sealed record V3Input
{
    public string? DeviceId { get; init; }

    public System.Text.Json.JsonElement? Channels { get; init; }
}

internal sealed record V3Instrument
{
    public string? Type { get; init; }

    public string? Preset { get; init; }

    public string? Osc1 { get; init; }

    public string? Osc2 { get; init; }

    public double? Ratio2 { get; init; }

    public double? Mix2 { get; init; }

    public double? Detune { get; init; }

    public double? Cutoff { get; init; }

    public double? Resonance { get; init; }

    public double? FilterEnv { get; init; }

    public double? Attack { get; init; }

    public double? Decay { get; init; }

    public double? Sustain { get; init; }

    public double? Release { get; init; }

    public double? Gain { get; init; }

    public string? Name { get; init; }

    public string? SampleKey { get; init; }

    public string? SampleName { get; init; }

    public int? RootNote { get; init; }

    public bool? OneShot { get; init; }
}

internal sealed record V3Fx
{
    public string? Type { get; init; }

    public bool? Enabled { get; init; }

    public Dictionary<string, System.Text.Json.JsonElement>? Params { get; init; }
}

internal sealed record V3Automation
{
    public List<V3Point>? Volume { get; init; }

    public List<V3Point>? Pan { get; init; }
}

internal sealed record V3Point
{
    public double? T { get; init; }

    public double? V { get; init; }
}

internal sealed record V3Send
{
    public string? BusId { get; init; }

    public double? Level { get; init; }

    public bool? Pre { get; init; }

    public bool? Muted { get; init; }
}

internal sealed record V3Clip
{
    public string? Id { get; init; }

    public string? BlobKey { get; init; }

    public string? Name { get; init; }

    public double? StartTime { get; init; }

    public double? Duration { get; init; }

    public double? Offset { get; init; }

    public double? TrimEnd { get; init; }

    public double? Gain { get; init; }

    public double? FadeIn { get; init; }

    public double? FadeOut { get; init; }

    public bool? Muted { get; init; }

    public string? Color { get; init; }

    // The version 4 meta type reads the version 3 shape; clip ids inside it still hold version 3 ids until remapped.
    public ClipMeta? Meta { get; init; }
}

internal sealed record V3MidiClip
{
    public string? Id { get; init; }

    public string? Name { get; init; }

    public double? StartTime { get; init; }

    public double? Length { get; init; }

    public List<V3Note>? Notes { get; init; }

    public List<V3Cc>? Cc { get; init; }

    public bool? Muted { get; init; }

    public string? Color { get; init; }
}

internal sealed record V3Note
{
    public double? Id { get; init; }

    public double? Pitch { get; init; }

    public double? Start { get; init; }

    public double? Dur { get; init; }

    public double? Vel { get; init; }
}

internal sealed record V3Cc
{
    public double? T { get; init; }

    public double? N { get; init; }

    public double? V { get; init; }
}
