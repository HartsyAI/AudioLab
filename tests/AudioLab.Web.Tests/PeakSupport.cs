using System.Net;
using System.Net.Http.Headers;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;
using AudioLab.Web.Services;
using AudioLab.Web.Waveforms;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Web.Tests;

/// <summary>What the peak tests share: a media store that serves peak files by range, stores built over it, and waits that need no clock.</summary>
internal static class PeakSupport
{
    private static readonly Uri _site = new("http://audiolab.test/");

    /// <summary>A peak file of a mono signal of the given length.</summary>
    public static byte[] PeakBytes(int frames, int rate = 44100)
    {
        float[] samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            samples[frame] = (frame % 100) / 200f;
        }
        PeakBuilder builder = new(1, rate);
        builder.AddFrames(samples);
        return PeakFile.Write(builder.Build());
    }

    /// <summary>A 206 answer with the bytes of the range the request named.</summary>
    public static HttpResponseMessage Slice(HttpRequestMessage request, byte[] file)
    {
        RangeItemHeaderValue range = request.Headers.Range!.Ranges.Single();
        int from = (int)range.From!.Value;
        int to = (int)Math.Min(range.To!.Value, file.Length - 1);
        HttpResponseMessage response = new(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(file[from..(to + 1)]) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(PeakFile.MediaType);
        return response;
    }

    /// <summary>A refusal with the server's problem details, and a Retry-After when one is given.</summary>
    public static HttpResponseMessage Refusal(HttpStatusCode status, string title, string? retryAfter = null)
    {
        HttpResponseMessage response = new(status)
        {
            Content = new StringContent($"{{\"title\":\"{title}\"}}", System.Text.Encoding.UTF8, "application/problem+json"),
        };
        if (retryAfter is not null)
        {
            response.Headers.Add("Retry-After", retryAfter);
        }
        return response;
    }

    /// <summary>A handler that never answers, for a request that is still on its way.</summary>
    public sealed class NeverHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    /// <summary>The client of the media routes over a handler.</summary>
    public static MediaApi Api(HttpMessageHandler handler)
    {
        return new MediaApi(new HttpClient(handler, false) { BaseAddress = _site }, new HttpClient(handler, false) { BaseAddress = _site });
    }

    /// <summary>A peak store over a handler.</summary>
    public static PeakStore Store(HttpMessageHandler handler, TimeProvider? clock = null, long? budget = null)
    {
        return new PeakStore(Api(handler), clock ?? TimeProvider.System, NullLogger<PeakStore>.Instance, budget ?? PeakStore.Budget);
    }

    /// <summary>Waits for the next arrival for a file; the bound is only how long a broken test may take to fail.</summary>
    public static async Task<PeakView> SettledAsync(PeakStore store, string media, double pixelsPerSecond)
    {
        while (true)
        {
            TaskCompletionSource arrival = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(string arrived)
            {
                if (arrived == media)
                {
                    arrival.TrySetResult();
                }
            }
            store.Arrived += Handler;
            try
            {
                PeakView view = store.Get(media, pixelsPerSecond);
                if (view.Phase != WavePhase.Loading)
                {
                    return view;
                }
                await arrival.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally
            {
                store.Arrived -= Handler;
            }
        }
    }

    /// <summary>Waits until a clip has a waveform that is not loading, for a service whose store is the one it was built over.</summary>
    public static async Task<ClipWave> SettledAsync(WaveformService service, AudioClip clip, double width, double pixelsPerSecond = 34)
    {
        while (true)
        {
            TaskCompletionSource arrival = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(string arrived)
            {
                if (arrived == clip.Media.Value)
                {
                    arrival.TrySetResult();
                }
            }
            service.Arrived += Handler;
            try
            {
                ClipWave wave = service.For(clip, width, pixelsPerSecond);
                if (wave.Phase != WavePhase.Loading)
                {
                    return wave;
                }
                await arrival.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally
            {
                service.Arrived -= Handler;
            }
        }
    }

    /// <summary>Moves a manual clock on, again and again, until the handler has seen as many requests as the test waits for.</summary>
    public static async Task AdvanceUntilAsync(ManualClock clock, RecordingHandler handler, int requests, TimeSpan step)
    {
        for (int turn = 0; turn < 100_000 && handler.Requests.Count < requests; turn++)
        {
            clock.Advance(step);
            await Task.Yield();
        }
        Assert.True(handler.Requests.Count >= requests, "The request did not come.");
    }
}
