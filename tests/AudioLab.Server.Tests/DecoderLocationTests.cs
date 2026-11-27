using System.Net;
using AudioLab.Audio.Tests.Support;
using AudioLab.Core.Model;

namespace AudioLab.Server.Tests;

public sealed class DecoderLocationTests
{
    /// <summary>A store whose decoder folder cannot be made still starts, and a WAV upload is stored as before.</summary>
    [Fact]
    public async Task TheServerStartsAndWavUploadsWorkWhenTheDecoderFolderCannotBeMade()
    {
        using TempFolder store = new();
        // A file where the decoder folder belongs: creating the folder and its temporary folder then fails.
        File.WriteAllText(Path.Combine(store.Path, "decoder"), "not a folder");
        await using RunningServer server = await RunningServer.StartAsync(new Dictionary<string, string?> { ["AudioLab:Storage:Root"] = store.Path });

        using HttpResponseMessage response = await DecoderTestSupport.UploadAsync(
            server,
            TestWav.Build(SampleEncoding.Pcm16, 2, 48000, 24000),
            "ok.wav",
            "audio/wav");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
