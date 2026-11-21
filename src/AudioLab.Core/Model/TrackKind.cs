using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>What a track holds and how it is played.</summary>
[JsonConverter(typeof(StrictEnumConverter<TrackKind>))]
public enum TrackKind
{
    /// <summary>Plays audio clips.</summary>
    [JsonStringEnumMemberName("audio")]
    Audio,

    /// <summary>Plays MIDI clips through an instrument.</summary>
    [JsonStringEnumMemberName("instrument")]
    Instrument,

    /// <summary>Has no clips and sums what other tracks send it.</summary>
    [JsonStringEnumMemberName("bus")]
    Bus,
}
