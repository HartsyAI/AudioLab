using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

/// <summary>Media records for tests: a valid one, and a catalog that holds whatever a test gives it.</summary>
public static class TestMedia
{
    public static MediaRecord Record(string id = "med_1", UserId? owner = null, double duration = 2.5, string name = "kick.wav")
    {
        return new MediaRecord
        {
            Id = MediaId.From(id),
            Owner = owner ?? UserId.Local,
            Name = name,
            Kind = MediaKind.Audio,
            Format = MediaFormat.Wav,
            Encoding = SampleEncoding.Pcm16,
            Duration = duration,
            SampleRate = 48000,
            Channels = 2,
            Bytes = 480_044,
            Sha256 = new string('a', 64),
            Created = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero),
        };
    }
}
