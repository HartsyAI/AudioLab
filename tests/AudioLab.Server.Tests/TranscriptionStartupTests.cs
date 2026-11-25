using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

/// <summary>The checks that run when the server starts: a registry entry that cannot work stops the start, so no job is accepted under it.</summary>
public sealed class TranscriptionStartupTests
{
    [Fact]
    public void ARegistryEntryWithABlankIdStopsTheStart()
    {
        ServiceCollection services = new();
        SpeechModelEntry blank = new(" ", SpeechModelCatalog.SpeechCapability, "folder", "en");

        Assert.Throws<ArgumentException>(() => services.AddTranscription(new TranscriptionOptions(), [blank]));
    }

    [Fact]
    public void ARegistryEntryWithAnIdTooLongStopsTheStart()
    {
        ServiceCollection services = new();
        SpeechModelEntry tooLong = new(
            new string('m', TranscriptionLimits.MaxModelIdChars + 1),
            SpeechModelCatalog.SpeechCapability,
            "folder",
            "en");

        Assert.Throws<ArgumentException>(() => services.AddTranscription(new TranscriptionOptions(), [tooLong]));
    }

    [Fact]
    public void AGoodRegistryEntryStartsTheServices()
    {
        ServiceCollection services = new();
        SpeechModelEntry good = new("model", SpeechModelCatalog.SpeechCapability, "folder", "en");

        services.AddTranscription(new TranscriptionOptions(), [good]);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(SpeechModelEntry));
    }
}
