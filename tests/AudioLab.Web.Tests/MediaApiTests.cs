using System.Net;
using System.Text;
using System.Text.Json;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the media api tests.</summary>
public sealed class MediaApiTests
{
    private const string Summary =
        "{\"id\":\"m1\",\"name\":\"kick.wav\",\"kind\":\"audio\",\"format\":\"wav\",\"encoding\":\"pcm16\",\"duration\":1.5,"
        + "\"sampleRate\":44100,\"channels\":2,\"bytes\":264644,\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\","
        + "\"created\":\"2026-11-22T10:00:00+00:00\"}";

    private static readonly Uri _site = new("http://audiolab.test/audiolab/");

    private static MediaApi Api(HttpMessageHandler handler)
    {
        return new MediaApi(new HttpClient(handler, false) { BaseAddress = _site }, new HttpClient(handler, false) { BaseAddress = _site });
    }

    /// <summary>The list is read from the route under the path prefix, with the count of records left out.</summary>
    [Fact]
    public async Task TheListIsReadFromTheRouteUnderThePathPrefix()
    {
        StubHandler handler = new(HttpStatusCode.OK, $"{{\"files\":[{Summary}],\"refused\":3}}");

        MediaListing listing = await Api(handler).ListAsync();

        Assert.Equal("/audiolab/api/media", handler.Requests.Single().Path);
        MediaSummary file = Assert.Single(listing.Files);
        Assert.Equal(("m1", 1.5, 44100, 2), (file.Id, file.Duration, file.SampleRate, file.Channels));
        Assert.Equal(3, listing.Refused);
    }

    /// <summary>The limits are read from their route, and a member this client does not know is refused like any other answer.</summary>
    [Fact]
    public async Task TheLimitsAreReadFromTheirRouteAndStrictly()
    {
        const string Limits = "{\"maxUploadBytes\":536870912,\"maxFiles\":5000,\"maxBytes\":21474836480,\"maxConcurrentUploads\":4,"
            + "\"files\":12,\"bytes\":83886080,\"filesRemaining\":4988,\"bytesRemaining\":21390950400,\"uploadsRunning\":1}";
        StubHandler handler = new(HttpStatusCode.OK, Limits);

        MediaLimits limits = await Api(handler).LimitsAsync();

        Assert.Equal("/audiolab/api/media/limits", handler.Requests.Single().Path);
        Assert.Equal((536870912L, 4988, 21390950400L), (limits.MaxUploadBytes, limits.FilesRemaining, limits.BytesRemaining));
        Assert.Equal(1, limits.UploadsRunning);
        StubHandler extra = new(HttpStatusCode.OK, Limits.Replace("{", "{\"owner\":\"u1\",", StringComparison.Ordinal));
        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(extra).LimitsAsync());
        Assert.Equal("The server answered with something this page cannot read.", failure.Message);
    }

    /// <summary>A summary keeps the duration the server measured exactly, since a clip must send it back verbatim.</summary>
    [Fact]
    public async Task ASummaryKeepsTheDurationExactly()
    {
        string exact = Summary.Replace("\"duration\":1.5", "\"duration\":0.30000000000000004", StringComparison.Ordinal);
        StubHandler handler = new(HttpStatusCode.OK, exact);

        MediaSummary file = await Api(handler).GetAsync("m1");

        Assert.Equal(0.30000000000000004, file.Duration);
        Assert.Equal("/audiolab/api/media/m1", handler.Requests.Single().Path);
    }

    /// <summary>An upload sends the raw bytes as audio/wav with the name escaped in the query and the length announced.</summary>
    [Fact]
    public async Task AnUploadSendsTheRawBytesAsWavWithTheNameEscapedAndTheLengthAnnounced()
    {
        byte[] file = [.. Enumerable.Range(0, 300_000).Select(index => (byte)(index % 251))];
        RecordingHandler handler = RecordingHandler.Always(HttpStatusCode.Created, Encoding.UTF8.GetBytes(Summary), "application/json");
        List<UploadProgress> reports = [];

        MediaSummary created = await Api(handler).UploadAsync(
            "kick & snare #1.wav",
            new MemoryStream(file),
            file.Length,
            new SyncProgress(reports.Add));

        SentRequest sent = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("/audiolab/api/media?name=kick%20%26%20snare%20%231.wav", sent.Path);
        Assert.Equal("audio/wav", sent.ContentType);
        Assert.Equal(file.Length, sent.ContentLength);
        Assert.Equal(file, sent.Body);
        Assert.Equal("m1", created.Id);
        Assert.NotEmpty(reports);
        Assert.Equal(new UploadProgress(file.Length, file.Length), reports[^1]);
        Assert.Equal(reports.Select(report => report.BytesRead).Order(), reports.Select(report => report.BytesRead));
    }

    /// <summary>A connection that closes before any answer is an interruption that says a refusal may be the cause.</summary>
    [Fact]
    public async Task AConnectionThatClosesBeforeAnyAnswerIsAnInterruption()
    {
        RecordingHandler handler = new(_ => throw new HttpRequestException("Failed to fetch"));

        UploadInterruptedException failure = await Assert.ThrowsAsync<UploadInterruptedException>(
            () => Api(handler).UploadAsync("a.wav", new MemoryStream([1, 2, 3]), 3));

        Assert.Contains("too large", failure.Message, StringComparison.Ordinal);
        Assert.Equal(failure.Message, FailureText.Of(failure));
    }

    /// <summary>Each refusal of the media routes reaches the user with the server's own words.</summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound, "There is no such media file.")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "The file is larger than the 536870912 bytes the server accepts.")]
    [InlineData(HttpStatusCode.Conflict, "The file is still used by your project \"Song\"; remove it from your projects before deleting it.")]
    [InlineData(HttpStatusCode.TooManyRequests, "You already have 4 uploads running, the most allowed at once; wait for one to finish.")]
    [InlineData(HttpStatusCode.InsufficientStorage, "The server has no room left to store media.")]
    public async Task EachRefusalReachesTheUserWithTheServersOwnWords(HttpStatusCode status, string title)
    {
        StubHandler handler = new(status, $"{{\"title\":{JsonSerializer.Serialize(title)}}}", "application/problem+json");

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(handler).DeleteAsync("m1"));

        Assert.Equal(status, failure.Status);
        Assert.Equal(title, failure.Message);
        Assert.Null(failure.RetryAfter);
    }

    /// <summary>A file that is not accepted says why, from the detail the server adds to its title.</summary>
    [Fact]
    public async Task AFileThatIsNotAcceptedSaysWhy()
    {
        StubHandler handler = new(
            HttpStatusCode.UnprocessableEntity,
            "{\"title\":\"The file is not accepted.\",\"detail\":\"The data chunk is cut short.\"}",
            "application/problem+json");

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(handler).UploadAsync("a.wav", new MemoryStream([1]), 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, failure.Status);
        Assert.Equal("The file is not accepted. The data chunk is cut short.", failure.Message);
    }

    /// <summary>A 503 with a Retry-After says when to ask again, in the exception and in the sentence.</summary>
    [Theory]
    [InlineData("5", 5, "Try again in 5 s.")]
    [InlineData("0", 1, "Try again in 1 s.")]
    [InlineData("120", 120, "Try again in 2 min.")]
    [InlineData("999999", 3600, "Try again in 60 min.")]
    public async Task A503WithARetryAfterSaysWhenToAskAgain(string header, int seconds, string sentence)
    {
        StubHandler handler = new(
            HttpStatusCode.ServiceUnavailable,
            "{\"title\":\"The peaks are being rebuilt; ask again in a moment.\"}",
            "application/problem+json",
            ("Retry-After", header));

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(handler).GetPeakHeaderAsync("m1"));

        Assert.Equal(TimeSpan.FromSeconds(seconds), failure.RetryAfter);
        Assert.Equal($"The peaks are being rebuilt; ask again in a moment. {sentence}", failure.Message);
    }

    /// <summary>A 503 without a Retry-After carries none, and a 429 with one keeps it.</summary>
    [Fact]
    public async Task ARetryAfterIsOnlyKeptWhenTheServerSendsOne()
    {
        StubHandler bare = new(HttpStatusCode.ServiceUnavailable, "{\"title\":\"The media store is not available; try again.\"}");
        StubHandler busy = new(HttpStatusCode.TooManyRequests, "{\"title\":\"Too many.\"}", "application/problem+json", ("Retry-After", "7"));

        ApiException none = await Assert.ThrowsAsync<ApiException>(() => Api(bare).ListAsync());
        ApiException seven = await Assert.ThrowsAsync<ApiException>(() => Api(busy).ListAsync());

        Assert.Null(none.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(7), seven.RetryAfter);
    }

    /// <summary>The header and one level are each asked for as a range of the peak file and read with the Core reader.</summary>
    [Fact]
    public async Task TheHeaderAndOneLevelAreEachAskedForAsARangeAndReadWithCoresReader()
    {
        PeakBuilder builder = new(1, 44100);
        builder.AddFrames(Enumerable.Range(0, 5000).Select(frame => (frame % 50) / 100f).ToArray());
        byte[] file = PeakFile.Write(builder.Build());
        RecordingHandler handler = new(request =>
        {
            System.Net.Http.Headers.RangeItemHeaderValue range = request.Headers.Range!.Ranges.Single();
            int from = (int)range.From!.Value;
            int to = (int)Math.Min(range.To!.Value, file.Length - 1);
            return new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(file[from..(to + 1)]) };
        });

        PeakHeader header = await Api(handler).GetPeakHeaderAsync("m1");
        PeakLevel level = await Api(handler).GetPeakLevelAsync("m1", header, 1);

        Assert.Equal("/audiolab/api/media/m1/peaks", handler.Requests[0].Path);
        Assert.Equal("bytes=0-151", handler.Requests[0].Range);
        (long offset, long length) = PeakFile.LevelRange(header, 1);
        Assert.Equal($"bytes={offset}-{offset + length - 1}", handler.Requests[1].Range);
        Assert.Equal((1, 44100, 5000L), (header.Channels, header.SampleRate, header.Frames));
        Assert.Equal(256, level.FramesPerPeak);
        Assert.True(PeakFile.Read(file).Levels[1].Values.SequenceEqual(level.Values));
    }

    /// <summary>Bytes that are not a peak file, or a level of the wrong length, are an api exception.</summary>
    [Fact]
    public async Task BytesThatAreNotAPeakFileAreAnApiException()
    {
        RecordingHandler bad = RecordingHandler.Always(HttpStatusCode.PartialContent, [1, 2, 3], PeakFile.MediaType);
        PeakBuilder builder = new(1, 44100);
        builder.AddFrames(new float[5000]);
        PeakHeader header = PeakFile.ReadHeader(PeakFile.Write(builder.Build()));

        ApiException head = await Assert.ThrowsAsync<ApiException>(() => Api(bad).GetPeakHeaderAsync("m1"));
        ApiException level = await Assert.ThrowsAsync<ApiException>(() => Api(bad).GetPeakLevelAsync("m1", header, 1));

        Assert.Equal("The server sent peaks this page cannot read.", head.Message);
        Assert.Equal("The server sent peaks this page cannot read.", level.Message);
    }

    /// <summary>An answer that is not a listing is an api exception and not a parser error.</summary>
    [Theory]
    [InlineData("<html>bad gateway</html>")]
    [InlineData("{\"files\":null,\"refused\":0}")]
    [InlineData("null")]
    public async Task AnAnswerThatIsNotAListingIsAnApiException(string body)
    {
        StubHandler handler = new(HttpStatusCode.OK, body);

        ApiException failure = await Assert.ThrowsAsync<ApiException>(() => Api(handler).ListAsync());

        Assert.StartsWith("The server answered with", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A delete names the file in the address, escaped.</summary>
    [Fact]
    public async Task ADeleteNamesTheFileInTheAddress()
    {
        StubHandler handler = new(HttpStatusCode.NoContent, string.Empty);

        await Api(handler).DeleteAsync("m 2");

        Assert.Equal(HttpMethod.Delete, handler.Requests.Single().Method);
        Assert.Equal("/audiolab/api/media/m%202", handler.Requests.Single().Path);
    }

    private sealed class SyncProgress(Action<UploadProgress> report) : IProgress<UploadProgress>
    {
        public void Report(UploadProgress value) => report(value);
    }
}
