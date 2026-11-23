using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Audio.Tests.Support;

/// <summary>The busy project with every kind of effect on it: a chain on a track, a reverb on the other, and a reverb on the bus.</summary>
public static class EffectsProject
{
    public static ProjectDocument Create()
    {
        ProjectDocument busy = BusyProject.Create();
        Effect Make(EffectKind kind, string id) => EffectRules.Create(kind, EffectId.From(id));
        EqEffect eq = (EqEffect)Make(EffectKind.Eq, "eq") with { LowGain = 4, MidGain = -3, MidFreq = 900, HighGain = 2, HpFreq = 60 };
        CompressorEffect compressor = (CompressorEffect)Make(EffectKind.Compressor, "comp") with { Threshold = -18, Makeup = 3 };
        DelayEffect delay = (DelayEffect)Make(EffectKind.Delay, "delay") with { Sync = true, Division = "1/8", Feedback = 0.45 };
        SaturationEffect saturation = (SaturationEffect)Make(EffectKind.Saturation, "sat") with { Drive = 0.6, Mix = 0.7 };
        ReverbEffect room = (ReverbEffect)Make(EffectKind.Reverb, "room") with { Size = 0.3, Decay = 2, Mix = 0.35 };
        ReverbEffect hall = (ReverbEffect)Make(EffectKind.Reverb, "hall") with { Size = 0.9, Decay = 3, Predelay = 0.02, Mix = 1 };
        Track first = busy.Tracks[0] with { Effects = [eq, compressor, saturation, delay] };
        Track second = busy.Tracks[1] with { Effects = [room] };
        Track bus = busy.Tracks[2] with { Effects = [hall] };
        return busy with
        {
            Tracks = new ValueList<Track>([first, second, bus, .. busy.Tracks.Skip(3)]),
            TempoMap = new ValueList<TempoChange>([new TempoChange(TempoChangeId.From("tc"), 1.3, 90)]),
        };
    }
}
