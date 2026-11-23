using System.Globalization;
using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>The words a track and its effects are shown with, one place so the header, the strip and the tests agree.</summary>
public static class TrackText
{
    /// <summary>The source line of a track header: where an audio track records from, the sound of an instrument track, or the bus.</summary>
    public static string Source(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return track.Kind switch
        {
            TrackKind.Bus => "Aux bus",
            TrackKind.Instrument => track.Instrument switch
            {
                SynthInstrument synth => synth.Preset,
                SamplerInstrument sampler => sampler.Name,
                _ => "Instrument",
            },
            _ => track.Input.Channels switch
            {
                InputChannelMode.Stereo => "In stereo",
                InputChannelMode.Single => string.Create(CultureInfo.InvariantCulture, $"In {track.Input.Channel}"),
                _ => "In mono",
            },
        };
    }

    /// <summary>The name an effect slot shows.</summary>
    public static string Effect(Effect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return effect switch
        {
            EqEffect => "EQ",
            CompressorEffect => "Compressor",
            ReverbEffect => "Reverb",
            DelayEffect => "Delay",
            SaturationEffect => "Saturation",
            _ => "Effect",
        };
    }

    /// <summary>The pan as a strip writes it: C at the centre, otherwise L or R and the percent.</summary>
    public static string Pan(double pan)
    {
        int percent = (int)Math.Round(Math.Abs(pan) * 100);
        return percent == 0 ? "C" : string.Create(CultureInfo.InvariantCulture, $"{(pan < 0 ? 'L' : 'R')}{percent}");
    }

    /// <summary>The label of a track's number in the header column: its place in the list, from 1.</summary>
    public static string Number(int index) => (index + 1).ToString(CultureInfo.InvariantCulture);
}
