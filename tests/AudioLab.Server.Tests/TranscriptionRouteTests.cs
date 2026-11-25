using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AudioLab.Core.Transcription;
using AudioLab.Server.Transcription;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

/// <summary>The transcription routes over HTTP: each status code the design names (D97), through the real server.</summary>
public sealed class TranscriptionRouteTests
{
    private const string UnknownId = "med_00000000000000000000000000000000";

    [Fact]
    public async Task AnUnknownMediaIsNotFound()
    {
        await using RunningServer server = await RunningServer.StartAsync();

        HttpResponseMessage missing = await server.Client.GetAsync($"/api/media/{UnknownId}/transcription");
        HttpResponseMessage malformed = await server.Client.GetAsync("/api/media/not-an-id/transcription");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, malformed.StatusCode);
    }

    [Fact]
    public async Task AStartIsCreatedOnceAndThenAnsweredWithTheJobThatIsThere()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?> { ["AudioLab:Transcription:ModelTimeout"] = "00:00:05", ["AudioLab:Transcription:Retries"] = "0" },
            services => services.AddSingleton<ISpeechModel>(new HangingSpeechModel()));
        string id = await UploadAsync(server.Client, "clip.wav");

        HttpResponseMessage first = await server.Client.PostAsync($"/api/media/{id}/transcription", null);
        HttpResponseMessage second = await server.Client.PostAsync($"/api/media/{id}/transcription", null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task ATranscriptThatIsNotDoneIsAConflictAndANotReadableOneIsRefused()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            null,
            services => services.AddSingleton<ISpeechModel>(new FakeSpeechModel()));
        string id = await UploadAsync(server.Client, "clip.wav");
        await server.Client.PostAsync($"/api/media/{id}/transcription", null);

        HttpResponseMessage early = await server.Client.GetAsync($"/api/media/{id}/transcript");
        Assert.True(early.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.OK, early.StatusCode.ToString());

        await WaitForStateAsync(server.Client, id, "done");
        string stored = Directory.EnumerateFiles(server.StoreFolder, "t*.transcript.json", SearchOption.AllDirectories).Single();
        File.WriteAllText(stored, "not json at all");
        HttpResponseMessage refused = await server.Client.GetAsync($"/api/media/{id}/transcript");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
    }

    [Fact]
    public async Task ATranscriptOfAJobThatHasNoTranscriptYetIsAConflict()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?> { ["AudioLab:Transcription:ModelTimeout"] = "00:00:05", ["AudioLab:Transcription:Retries"] = "0" },
            services => services.AddSingleton<ISpeechModel>(new HangingSpeechModel()));
        string id = await UploadAsync(server.Client, "clip.wav");
        await server.Client.PostAsync($"/api/media/{id}/transcription", null);

        HttpResponseMessage transcript = await server.Client.GetAsync($"/api/media/{id}/transcript");

        Assert.Equal(HttpStatusCode.Conflict, transcript.StatusCode);
    }

    [Fact]
    public async Task TheLimitOfQueuedJobsIsTooManyRequests()
    {
        await using RunningServer server = await RunningServer.StartAsync(
            new Dictionary<string, string?>
            {
                ["AudioLab:Transcription:MaxQueuedPerUser"] = "1",
                ["AudioLab:Transcription:ModelTimeout"] = "00:00:05",
                ["AudioLab:Transcription:Retries"] = "0",
            },
            services => services.AddSingleton<ISpeechModel>(new HangingSpeechModel()));
        string first = await UploadAsync(server.Client, "one.wav");
        string second = await UploadAsync(server.Client, "two.wav");
        await server.Client.PostAsync($"/api/media/{first}/transcription", null);

        HttpResponseMessage refused = await server.Client.PostAsync($"/api/media/{second}/transcription", null);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    [Fact]
    public async Task AStoreThatCannotBeWrittenIsServiceUnavailableAndAFullDiskIsInsufficientStorage()
    {
        await using RunningServer unavailable = await RunningServer.StartAsync(
            null,
            services => services.AddSingleton<IAudioWindowSource>(new BrokenWindowSource(10, new IOException("The store is locked."), onDuration: true)));
        string id = await UploadAsync(unavailable.Client, "clip.wav");
        HttpResponseMessage answer = await unavailable.Client.PostAsync($"/api/media/{id}/transcription", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, answer.StatusCode);

        await using RunningServer full = await RunningServer.StartAsync(
            null,
            services => services.AddSingleton<IAudioWindowSource>(new BrokenWindowSource(10, new IOException("No space.", unchecked((int)0x80070070)), onDuration: true)));
        string fullId = await UploadAsync(full.Client, "clip.wav");
        HttpResponseMessage outOfRoom = await full.Client.PostAsync($"/api/media/{fullId}/transcription", null);
        Assert.Equal(HttpStatusCode.InsufficientStorage, outOfRoom.StatusCode);
    }

    private static async Task<string> UploadAsync(HttpClient client, string name)
    {
        using ByteArrayContent content = new(TranscriptionTestSupport.Wav());
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        HttpResponseMessage response = await client.PostAsync($"/api/media?name={name}", content);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task WaitForStateAsync(HttpClient client, string id, string state)
    {
        for (int attempt = 0; attempt < 500; attempt++)
        {
            HttpResponseMessage response = await client.GetAsync($"/api/media/{id}/transcription");
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.GetProperty("state").GetString() == state)
            {
                return;
            }
            await Task.Delay(20);
        }
        Assert.Fail($"The job never reached {state}.");
    }
}
