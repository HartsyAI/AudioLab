using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

// The shape of the extension's version 3 project file: serializeProject and serializeTrack (audio-daw.js,
// audio-daw-track.js) plus what saveProjectToServer adds, the "audio" list (audio-daw.js:9663); projects saved before
// audio moved to its own files carry "blobs" instead (read by openProjectFromServer, audio-daw.js:9707). A field the
// extension might leave out is nullable; a field it never wrote is not here, so the strict V3 context refuses it.

/// <summary>A version 3 project.</summary>
internal sealed record V3Project
{
    /// <summary>The "version" member.</summary>
    public int? Version { get; init; }

    /// <summary>The "audio" member.</summary>
    public Dictionary<string, V3AudioInfo>? Audio { get; init; }

    /// <summary>The "blobs" member.</summary>
    public Dictionary<string, V3InlineBlob>? Blobs { get; init; }

    /// <summary>The "projectName" member.</summary>
    public string? ProjectName { get; init; }

    /// <summary>The "bpm" member.</summary>
    public double? Bpm { get; init; }

    /// <summary>The "tempoMap" member.</summary>
    public List<V3Tempo>? TempoMap { get; init; }

    /// <summary>The "masterLimiterEnabled" member.</summary>
    public bool? MasterLimiterEnabled { get; init; }

    /// <summary>The "timeSignature" member.</summary>
    public List<int>? TimeSignature { get; init; }

    /// <summary>The "masterVolume" member.</summary>
    public double? MasterVolume { get; init; }

    /// <summary>The "rulerMode" member.</summary>
    public string? RulerMode { get; init; }

    /// <summary>The "snapEnabled" member.</summary>
    public bool? SnapEnabled { get; init; }

    /// <summary>The "editMode" member.</summary>
    public string? EditMode { get; init; }

    /// <summary>The "gridValue" member.</summary>
    public string? GridValue { get; init; }

    /// <summary>The "markers" member.</summary>
    public List<V3Marker>? Markers { get; init; }

    /// <summary>The "sections" member.</summary>
    public List<V3Section>? Sections { get; init; }

    /// <summary>The "groups" member.</summary>
    public List<V3Group>? Groups { get; init; }

    /// <summary>The "metronome" member.</summary>
    public bool? Metronome { get; init; }

    /// <summary>The "beatPattern" member.</summary>
    public V3BeatPattern? BeatPattern { get; init; }

    /// <summary>The "tracks" member.</summary>
    public List<V3Track>? Tracks { get; init; }
}

/// <summary>The version 3 file's audio info.</summary>
internal sealed record V3AudioInfo
{
    /// <summary>The "type" member.</summary>
    public string? Type { get; init; }

    /// <summary>The "size" member.</summary>
    public long? Size { get; init; }
}

/// <summary>The version 3 file's inline blob.</summary>
internal sealed record V3InlineBlob
{
    /// <summary>The "b64" member.</summary>
    public string? B64 { get; init; }

    /// <summary>The "type" member.</summary>
    public string? Type { get; init; }
}

/// <summary>The version 3 file's tempo.</summary>
internal sealed record V3Tempo
{
    /// <summary>The "id" member.</summary>
    public string? Id { get; init; }

    /// <summary>The "t" member.</summary>
    public double? T { get; init; }

    /// <summary>The "bpm" member.</summary>
    public double? Bpm { get; init; }
}

/// <summary>The version 3 file's marker.</summary>
internal sealed record V3Marker
{
    /// <summary>The "id" member.</summary>
    public string? Id { get; init; }

    /// <summary>The "time" member.</summary>
    public double? Time { get; init; }

    /// <summary>The "name" member.</summary>
    public string? Name { get; init; }

    /// <summary>The "color" member.</summary>
    public string? Color { get; init; }
}

/// <summary>The version 3 file's section.</summary>
internal sealed record V3Section
{
    /// <summary>The "id" member.</summary>
    public string? Id { get; init; }

    /// <summary>The "start" member.</summary>
    public double? Start { get; init; }

    /// <summary>The "end" member.</summary>
    public double? End { get; init; }

    /// <summary>The "name" member.</summary>
    public string? Name { get; init; }

    /// <summary>The "color" member.</summary>
    public string? Color { get; init; }
}

/// <summary>The version 3 file's group.</summary>
internal sealed record V3Group
{
    /// <summary>The "id" member.</summary>
    public string? Id { get; init; }

    /// <summary>The "name" member.</summary>
    public string? Name { get; init; }

    /// <summary>The "color" member.</summary>
    public string? Color { get; init; }

    /// <summary>The "collapsed" member.</summary>
    public bool? Collapsed { get; init; }

    /// <summary>The "tint" member.</summary>
    public bool? Tint { get; init; }

    /// <summary>The "muted" member.</summary>
    public bool? Muted { get; init; }

    /// <summary>The "soloed" member.</summary>
    public bool? Soloed { get; init; }

    /// <summary>The "volume" member.</summary>
    public double? Volume { get; init; }
}

/// <summary>The version 3 file's beat pattern.</summary>
internal sealed record V3BeatPattern
{
    /// <summary>The "steps" member.</summary>
    public int? Steps { get; init; }

    /// <summary>The "swing" member.</summary>
    public double? Swing { get; init; }

    /// <summary>The "lanes" member.</summary>
    public List<V3BeatLane>? Lanes { get; init; }
}

/// <summary>The version 3 file's beat lane.</summary>
internal sealed record V3BeatLane
{
    /// <summary>The "name" member.</summary>
    public string? Name { get; init; }

    /// <summary>The "blobKey" member.</summary>
    public string? BlobKey { get; init; }

    /// <summary>The "gain" member.</summary>
    public double? Gain { get; init; }

    /// <summary>The "steps" member.</summary>
    public List<System.Text.Json.JsonElement>? Steps { get; init; }
}

/// <summary>The version 3 file's track.</summary>
internal sealed record V3Track
{
    /// <summary>The "id" member.</summary>
    public string? Id { get; init; }

    /// <summary>The "name" member.</summary>
    public string? Name { get; init; }

    /// <summary>The "volume" member.</summary>
    public double? Volume { get; init; }

    /// <summary>The "pan" member.</summary>
    public double? Pan { get; init; }

    /// <summary>The "muted" member.</summary>
    public bool? Muted { get; init; }

    /// <summary>The "soloed" member.</summary>
    public bool? Soloed { get; init; }

    /// <summary>The "groupId" member.</summary>
    public string? GroupId { get; init; }

    /// <summary>The "icon" member.</summary>
    public string? Icon { get; init; }

    /// <summary>The "armed" member.</summary>
    public bool? Armed { get; init; }

    /// <summary>The "monitor" member.</summary>
    public bool? Monitor { get; init; }

    /// <summary>The "input" member.</summary>
    public V3Input? Input { get; init; }

    /// <summary>The "takeCount" member.</summary>
    public int? TakeCount { get; init; }

    /// <summary>The "kind" member.</summary>
    public string? Kind { get; init; }

    /// <summary>The "instrument" member.</summary>
    public V3Instrument? Instrument { get; init; }

    /// <summary>The "midiClips" member.</summary>
    public List<V3MidiClip>? MidiClips { get; init; }

    /// <summary>The "color" member.</summary>
    public string? Color { get; init; }

    /// <summary>The "height" member.</summary>
    public double? Height { get; init; }

    /// <summary>The "fx" member.</summary>
    public List<V3Fx>? Fx { get; init; }

    /// <summary>The "automation" member.</summary>
    public V3Automation? Automation { get; init; }

    /// <summary>The "automationVisible" member.</summary>
    public bool? AutomationVisible { get; init; }

    /// <summary>The "automationParam" member.</summary>
    public string? AutomationParam { get; init; }

    /// <summary>The "automationMode" member.</summary>
    public string? AutomationMode { get; init; }

    /// <summary>The "sends" member.</summary>
    public List<V3Send>? Sends { get; init; }

    /// <summary>The "clips" member.</summary>
    public List<V3Clip>? Clips { get; init; }
}

/// <summary>The version 3 file's input.</summary>
internal sealed record V3Input
{
    /// <summary>The "deviceId" member.</summary>
    public string? DeviceId { get; init; }

    /// <summary>The "channels" member.</summary>
    public System.Text.Json.JsonElement? Channels { get; init; }
}

/// <summary>The version 3 file's instrument.</summary>
internal sealed record V3Instrument
{
    /// <summary>The "type" member.</summary>
    public string? Type { get; init; }

    /// <summary>The "preset" member.</summary>
    public string? Preset { get; init; }

    /// <summary>The "osc1" member.</summary>
    public string? Osc1 { get; init; }

    /// <summary>The "osc2" member.</summary>
    public string? Osc2 { get; init; }

    /// <summary>The "ratio2" member.</summary>
    public double? Ratio2 { get; init; }

    /// <summary>The "mix2" member.</summary>
    public double? Mix2 { get; init; }

    /// <summary>The "detune" member.</summary>
    public double? Detune { get; init; }

    /// <summary>The "cutoff" member.</summary>
    public double? Cutoff { get; init; }

    /// <summary>The "resonance" member.</summary>
    public double? Resonance { get; init; }

    /// <summary>The "filterEnv" member.</summary>
    public double? FilterEnv { get; init; }

    /// <summary>The "attack" member.</summary>
    public double? Attack { get; init; }

    /// <summary>The "decay" member.</summary>
    public double? Decay { get; init; }

    /// <summary>The "sustain" member.</summary>
    public double? Sustain { get; init; }

    /// <summary>The "release" member.</summary>
    public double? Release { get; init; }

    /// <summary>The "gain" member.</summary>
    public double? Gain { get; init; }

    /// <summary>The "name" member.</summary>
    public string? Name { get; init; }

    /// <summary>The "sampleKey" member.</summary>
    public string? SampleKey { get; init; }

    /// <summary>The "sampleName" member.</summary>
    public string? SampleName { get; init; }

    /// <summary>The "rootNote" member.</summary>
    public int? RootNote { get; init; }

    /// <summary>The "oneShot" member.</summary>
    public bool? OneShot { get; init; }
}

/// <summary>The version 3 file's fx.</summary>
internal sealed record V3Fx
{
    /// <summary>The "type" member.</summary>
    public string? Type { get; init; }

    /// <summary>The "enabled" member.</summary>
    public bool? Enabled { get; init; }

    /// <summary>The "params" member.</summary>
    public Dictionary<string, System.Text.Json.JsonElement>? Params { get; init; }
}

/// <summary>The version 3 file's automation.</summary>
internal sealed record V3Automation
{
    /// <summary>The "volume" member.</summary>
    public List<V3Point>? Volume { get; init; }

    /// <summary>The "pan" member.</summary>
    public List<V3Point>? Pan { get; init; }
}

/// <summary>The version 3 file's point.</summary>
internal sealed record V3Point
{
    /// <summary>The "t" member.</summary>
    public double? T { get; init; }

    /// <summary>The "v" member.</summary>
    public double? V { get; init; }
}

/// <summary>The version 3 file's send.</summary>
internal sealed record V3Send
{
    /// <summary>The "busId" member.</summary>
    public string? BusId { get; init; }

    /// <summary>The "level" member.</summary>
    public double? Level { get; init; }

    /// <summary>The "pre" member.</summary>
    public bool? Pre { get; init; }

    /// <summary>The "muted" member.</summary>
    public bool? Muted { get; init; }
}

/// <summary>The version 3 file's clip.</summary>
internal sealed record V3Clip
{
    /// <summary>The "id" member.</summary>
    public string? Id { get; init; }

    /// <summary>The "blobKey" member.</summary>
    public string? BlobKey { get; init; }

    /// <summary>The "name" member.</summary>
    public string? Name { get; init; }

    /// <summary>The "startTime" member.</summary>
    public double? StartTime { get; init; }

    /// <summary>The "duration" member.</summary>
    public double? Duration { get; init; }

    /// <summary>The "offset" member.</summary>
    public double? Offset { get; init; }

    /// <summary>The "trimEnd" member.</summary>
    public double? TrimEnd { get; init; }

    /// <summary>The "gain" member.</summary>
    public double? Gain { get; init; }

    /// <summary>The "fadeIn" member.</summary>
    public double? FadeIn { get; init; }

    /// <summary>The "fadeOut" member.</summary>
    public double? FadeOut { get; init; }

    /// <summary>The "muted" member.</summary>
    public bool? Muted { get; init; }

    /// <summary>The "color" member.</summary>
    public string? Color { get; init; }

    // The version 4 meta type reads the version 3 shape; clip ids inside it still hold version 3 ids until remapped.
    /// <summary>The "meta" member.</summary>
    public ClipMeta? Meta { get; init; }
}

/// <summary>The version 3 file's midi clip.</summary>
internal sealed record V3MidiClip
{
    /// <summary>The "id" member.</summary>
    public string? Id { get; init; }

    /// <summary>The "name" member.</summary>
    public string? Name { get; init; }

    /// <summary>The "startTime" member.</summary>
    public double? StartTime { get; init; }

    /// <summary>The "length" member.</summary>
    public double? Length { get; init; }

    /// <summary>The "notes" member.</summary>
    public List<V3Note>? Notes { get; init; }

    /// <summary>The "cc" member.</summary>
    public List<V3Cc>? Cc { get; init; }

    /// <summary>The "muted" member.</summary>
    public bool? Muted { get; init; }

    /// <summary>The "color" member.</summary>
    public string? Color { get; init; }
}

/// <summary>The version 3 file's note.</summary>
internal sealed record V3Note
{
    /// <summary>The "id" member.</summary>
    public double? Id { get; init; }

    /// <summary>The "pitch" member.</summary>
    public double? Pitch { get; init; }

    /// <summary>The "start" member.</summary>
    public double? Start { get; init; }

    /// <summary>The "dur" member.</summary>
    public double? Dur { get; init; }

    /// <summary>The "vel" member.</summary>
    public double? Vel { get; init; }
}

/// <summary>The version 3 file's cc.</summary>
internal sealed record V3Cc
{
    /// <summary>The "t" member.</summary>
    public double? T { get; init; }

    /// <summary>The "n" member.</summary>
    public double? N { get; init; }

    /// <summary>The "v" member.</summary>
    public double? V { get; init; }
}
