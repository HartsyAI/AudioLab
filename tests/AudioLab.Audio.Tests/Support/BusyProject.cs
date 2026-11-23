using AudioLab.Core.Model;

namespace AudioLab.Audio.Tests.Support;

/// <summary>A project that uses most of the mixer at once: layered and faded clips, automation, sends, a bus, a group, the limiter.</summary>
public static class BusyProject
{
    public const int Rate = 8000;

    public static MemoryMedia Media() => new MemoryMedia()
        .Add("tone", Signals.Sine(440, 3, Rate, 0.7))
        .Add("pair", Signals.Stereo(Signals.Sine(300, 3, Rate, 0.6).Channel(0).ToArray(), Signals.Sine(500, 3, Rate, 0.6).Channel(0).ToArray()));

    /// <summary>The same sounds as <see cref="Media"/> but recorded at 11.025 kHz, so the engine has to resample them.</summary>
    public static MemoryMedia ResampledMedia() => new MemoryMedia()
        .Add("tone", Signals.Sine(440, 3, 11025, 0.7))
        .Add("pair", Signals.Stereo(Tone(300), Tone(500), 11025));

    private static float[] Tone(double hertz) => Signals.Sine(hertz, 3, 11025, 0.6).Channel(0).ToArray();

    public static ProjectDocument Create()
    {
        GroupId group = GroupId.From("g");
        Track first = TestProject.Track(
            "t1",
            TrackKind.Audio,
            TestProject.Clip("a", "tone", 0.1, 1.5) with { FadeIn = 0.2, FadeOut = 0.3, Gain = 1.2 },
            TestProject.Clip("b", "pair", 0.9, 1.2, offset: 0.4) with { FadeIn = 0.25 }) with
        {
            Pan = -0.3,
            Group = group,
            Sends = [new Send(TrackId.From("bus"), 0.6, false, false), new Send(TrackId.From("bus2"), 0.6, true, false)],
            Automation = new TrackAutomation
            {
                Mode = AutomationMode.Read,
                Volume = new ValueList<AutomationPoint>([new(0, 1), new(1, 0.4), new(2, 1.5)]),
                Pan = new ValueList<AutomationPoint>([new(0.5, -0.4), new(1.5, 0.6)]),
            },
        };
        Track other = TestProject.Track("t2", TrackKind.Audio, TestProject.Clip("c", "pair", 0, 2.5, gain: 0.9)) with
        {
            Volume = 0.8,
            Pan = 0.4,
            Group = group,
        };
        Track bus = TestProject.Track("bus", TrackKind.Bus) with { Volume = 0.9 };
        Track second = TestProject.Track("bus2", TrackKind.Bus);
        TrackGroup shared = new()
        {
            Id = group,
            Name = "g",
            Color = "#ffffff",
            Collapsed = false,
            Tint = false,
            Muted = false,
            Soloed = false,
            Volume = 0.85,
        };
        return TestProject.Create(first, other, bus, second) with { Groups = [shared], MasterVolume = 1.4, MasterLimiter = true };
    }
}
