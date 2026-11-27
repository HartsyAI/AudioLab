using System.Net;
using System.Text;
using AudioLab.Web.Media;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>The upload is sent as the file's own content type, and the picker and the decoder state agree with the server's rules.</summary>
public sealed class MediaUploadTypeTests
{
    private const string Summary =
        "{\"id\":\"m1\",\"name\":\"song.mp3\",\"kind\":\"audio\",\"format\":\"wav\",\"encoding\":\"pcm16\",\"duration\":1.5,"
        + "\"sampleRate\":48000,\"channels\":2,\"bytes\":288044,\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\","
        + "\"created\":\"2026-11-25T10:00:00+00:00\"}";

    private static readonly Uri _site = new("http://audiolab.test/audiolab/");

    /// <summary>The upload carries the content type the caller gives, so a compressed file is not sent as WAV.</summary>
    [Theory]
    [InlineData("audio/wav")]
    [InlineData("audio/mpeg")]
    [InlineData("audio/flac")]
    public async Task TheUploadIsSentAsTheFilesOwnContentType(string contentType)
    {
        TypeHandler handler = new();
        MediaApi api = new(new HttpClient(handler, false) { BaseAddress = _site }, new HttpClient(handler, false) { BaseAddress = _site });

        await api.UploadAsync("song", new MemoryStream([1, 2, 3]), 3, contentType: contentType);

        Assert.Equal(contentType, handler.Type);
    }

    /// <summary>A caller that names no type still sends WAV, as before.</summary>
    [Fact]
    public async Task AWavUploadStillDefaultsToWav()
    {
        TypeHandler handler = new();
        MediaApi api = new(new HttpClient(handler, false) { BaseAddress = _site }, new HttpClient(handler, false) { BaseAddress = _site });

        await api.UploadAsync("kick.wav", new MemoryStream([1, 2, 3]), 3);

        Assert.Equal(MediaTypes.Wav, handler.Type);
    }

    /// <summary>A chosen file is sent as the type its name shows, and an unknown one as the browser reports it.</summary>
    [Theory]
    [InlineData("song.mp3", "", "audio/mpeg")]
    [InlineData("SONG.MP3", "", "audio/mpeg")]
    [InlineData("take.m4a", "", "audio/mp4")]
    [InlineData("take.flac", "", "audio/flac")]
    [InlineData("take.ogg", "", "audio/ogg")]
    [InlineData("kick.wav", "", "audio/wav")]
    [InlineData("notes.txt", "text/plain", "text/plain")]
    [InlineData("odd.bin", "", "application/octet-stream")]
    public void AFileIsSentAsTheTypeItsNameShows(string name, string browserType, string expected)
    {
        Assert.Equal(expected, MediaTypes.FromName(name, browserType));
    }

    /// <summary>The chooser offers the compressed types only while the decoder is installed.</summary>
    [Fact]
    public void TheChooserOffersTheCompressedTypesOnlyWhileTheDecoderIsInstalled()
    {
        Assert.DoesNotContain(".mp3", MediaTypes.Accept(false), StringComparison.Ordinal);
        Assert.Contains(".mp3", MediaTypes.Accept(true), StringComparison.Ordinal);
        Assert.Contains("audio/wav", MediaTypes.Accept(false), StringComparison.Ordinal);
    }

    /// <summary>Only a WAV file is checked on the page before its upload.</summary>
    [Fact]
    public void OnlyWavSkipsTheWavCheckOnThePage()
    {
        Assert.True(MediaTypes.IsWav("audio/wav"));
        Assert.True(MediaTypes.IsWav("AUDIO/X-WAV"));
        Assert.False(MediaTypes.IsWav("audio/mpeg"));
    }

    /// <summary>The decoder state tells its listeners only when the installed flag changes.</summary>
    [Fact]
    public void TheDecoderStateTellsItsListenersOnlyOfAChange()
    {
        DecoderState state = new();
        int changes = 0;
        state.Changed += () => changes++;

        state.Set(false);
        state.Set(true);
        state.Set(true);

        Assert.True(state.Installed);
        Assert.Equal(1, changes);
    }

    private sealed class TypeHandler : HttpMessageHandler
    {
        public string? Type { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Type = request.Content?.Headers.ContentType?.MediaType;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(Summary, Encoding.UTF8, "application/json"),
            });
        }
    }
}
