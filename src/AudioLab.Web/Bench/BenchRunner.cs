using System.Net.Http.Json;
using AudioLab.Web.Interop;
using AudioLab.Web.Rendering;
using AudioLab.Web.Session;

namespace AudioLab.Web.Bench;

/// <summary>Drives the scripted scroll and drag scenarios through the same view and drag objects real input uses, then posts the result.</summary>
public sealed class BenchRunner(
    IBrowser browser,
    FrameRecorder recorder,
    TimelineView view,
    DragController drag,
    SpikeOptions options,
    SkiaDiagnostics skia,
    HttpClient http)
{
    private const double WarmupSeconds = 1;

    private const double RunSeconds = 6;

    /// <summary>Runs every scenario the configured number of times and posts the report.</summary>
    public async Task RunAsync()
    {
        using BrowserElement scroller = browser.Find("timeline") ?? throw new InvalidOperationException("The timeline is not in the page.");
        List<FrameStats> results = [];
        for (int run = 0; run < options.Runs; run++)
        {
            if (options.Scenarios == "pickup")
            {
                results.Add(await RunPickupAsync(scroller));
                continue;
            }
            if (options.Scenarios == "drag")
            {
                results.Add(await RunDragAsync(scroller));
                results.Add(await RunPickupAsync(scroller));
                continue;
            }
            results.Add(await RunAsync(scroller, "scroll-horizontal", t => scroller.ScrollLeft = Sweep(t, 2400, view.ContentWidth - view.ViewWidth)));
            results.Add(await RunAsync(scroller, "scroll-vertical", t => scroller.ScrollTop = Sweep(t, 1200, view.ContentHeight - view.ViewHeight)));
            results.Add(await RunAsync(scroller, "scroll-diagonal", t =>
            {
                scroller.ScrollLeft = Sweep(t, 1800, view.ContentWidth - view.ViewWidth);
                scroller.ScrollTop = Sweep(t, 900, view.ContentHeight - view.ViewHeight);
            }));
            results.Add(await RunDragAsync(scroller));
        }
        BenchReport report = new()
        {
            Label = options.Label,
            Renderer = options.Renderer.ToString(),
            UserAgent = browser.UserAgent,
            DevicePixelRatio = browser.DevicePixelRatio,
            WindowWidth = browser.WindowWidth,
            WindowHeight = browser.WindowHeight,
            BarWidth = options.BarWidth,
            Tracks = view.Session.Tracks.Count,
            Clips = view.Session.Clips.Count,
            ViewWidth = view.ViewWidth,
            ViewHeight = view.ViewHeight,
            SkiaSurface = skia.LastSurface,
            FontFailures = [.. skia.FontFailures],
            Results = [.. results],
        };
        await http.PostAsJsonAsync("bench-result", report, BenchJsonContext.Default.BenchReport);
    }

    private static double Sweep(double seconds, double speed, double range)
    {
        if (range <= 0)
        {
            return 0;
        }
        double distance = seconds * speed % (2 * range);
        return distance <= range ? distance : (2 * range) - distance;
    }

    private async Task<FrameStats> RunPickupAsync(BrowserElement scroller)
    {
        scroller.ScrollLeft = 0;
        scroller.ScrollTop = 0;
        await Task.Delay(600);
        ClipModel clip = view.Session.Clips.First(candidate => candidate.Name == "Rhythm guitar");
        double startX = view.ClipLeft(clip) + 60;
        double startY = (clip.Track * view.LaneHeight) + (view.LaneHeight / 2);
        int lastStep = -1;
        FrameStats stats = await RunFramesAsync("drag-pickup-and-drop", t =>
        {
            int step = (int)(t * 8);
            if (step == lastStep)
            {
                return;
            }
            lastStep = step;
            switch (step % 4)
            {
                case 0:
                    drag.Begin(clip, startX, startY);
                    break;
                case 1:
                    drag.Move(startX + 80, startY + 30);
                    break;
                case 2:
                    drag.Move(startX, startY);
                    break;
                default:
                    drag.Commit();
                    break;
            }
        });
        if (drag.Clip is not null)
        {
            drag.Cancel();
        }
        return stats;
    }

    private async Task<FrameStats> RunDragAsync(BrowserElement scroller)
    {
        scroller.ScrollLeft = 0;
        scroller.ScrollTop = 0;
        await Task.Delay(600);
        ClipModel clip = view.Session.Clips.First(candidate => candidate.Name == "Rhythm guitar");
        double startX = view.ClipLeft(clip) + 60;
        double startY = (clip.Track * view.LaneHeight) + (view.LaneHeight / 2);
        drag.Begin(clip, startX, startY);
        FrameStats stats = await RunFramesAsync("drag-clip", t => drag.Move(
            startX + (320 * Math.Sin(t * 2.1)),
            startY + (90 * Math.Sin(t * 2.9))));
        drag.Cancel();
        return stats;
    }

    private async Task<FrameStats> RunAsync(BrowserElement scroller, string name, Action<double> step)
    {
        scroller.ScrollLeft = 0;
        scroller.ScrollTop = 0;
        await Task.Delay(600);
        FrameStats stats = await RunFramesAsync(name, step);
        scroller.ScrollLeft = 0;
        scroller.ScrollTop = 0;
        return stats;
    }

    private async Task<FrameStats> RunFramesAsync(string name, Action<double> step)
    {
        TaskCompletionSource finished = new();
        double begin = -1;
        bool warmed = false;
        recorder.Start(timestamp =>
        {
            if (begin < 0)
            {
                begin = timestamp;
            }
            double elapsed = (timestamp - begin) / 1000;
            if (!warmed && elapsed >= WarmupSeconds)
            {
                warmed = true;
                recorder.Clear();
            }
            if (elapsed >= WarmupSeconds + RunSeconds)
            {
                finished.TrySetResult();
                return;
            }
            step(elapsed);
        });
        await finished.Task;
        return recorder.Stop(name);
    }
}
