using AudioLab.Web.Interop;
using AudioLab.Web.Playback;
using AudioLab.Web.Session;

namespace AudioLab.Web.Bench;

/// <summary>Scrolls and drags the timeline in a loop through the same view and drag objects real input uses, for as long as it is asked to.</summary>
public sealed class LoadRunner(IBrowser browser, FrameRecorder recorder, TimelineView view, DragController drag)
{
    private const double ScenarioSeconds = 6;

    private int _scenarios;

    private long _frames;

    private long _over100;

    private double _maxFrameMs;

    private double _worstSecond = double.MaxValue;

    /// <summary>What the load has done so far.</summary>
    public LoadSummary Summary => new()
    {
        Scenarios = _scenarios,
        Frames = _frames,
        FramesOver100Ms = _over100,
        MaxFrameMs = _maxFrameMs,
        WorstSecondFps = _worstSecond == double.MaxValue ? 0 : _worstSecond,
    };

    /// <summary>Runs the scenarios one after another until cancelled.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using BrowserElement scroller = browser.Find("timeline") ?? throw new InvalidOperationException("The timeline is not in the page.");
        while (!cancellationToken.IsCancellationRequested)
        {
            await ScrollAsync(scroller, t => scroller.ScrollLeft = Sweep(t, 2400, view.ContentWidth - view.ViewWidth));
            await ScrollAsync(scroller, t => scroller.ScrollTop = Sweep(t, 1200, view.ContentHeight - view.ViewHeight));
            await ScrollAsync(scroller, t =>
            {
                scroller.ScrollLeft = Sweep(t, 1800, view.ContentWidth - view.ViewWidth);
                scroller.ScrollTop = Sweep(t, 900, view.ContentHeight - view.ViewHeight);
            });
            await DragAsync(scroller);
            await PickupAsync(scroller);
        }
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

    private async Task ScrollAsync(BrowserElement scroller, Action<double> step)
    {
        scroller.ScrollLeft = 0;
        scroller.ScrollTop = 0;
        await RunFramesAsync(step);
    }

    private async Task DragAsync(BrowserElement scroller)
    {
        scroller.ScrollLeft = 0;
        scroller.ScrollTop = 0;
        ClipModel clip = view.Session.Clips.First(candidate => candidate.Name == "Rhythm guitar");
        double startX = view.ClipLeft(clip) + 60;
        double startY = (clip.Track * view.LaneHeight) + (view.LaneHeight / 2);
        drag.Begin(clip, startX, startY);
        await RunFramesAsync(t => drag.Move(startX + (320 * Math.Sin(t * 2.1)), startY + (90 * Math.Sin(t * 2.9))));
        drag.Cancel();
    }

    private async Task PickupAsync(BrowserElement scroller)
    {
        scroller.ScrollLeft = 0;
        scroller.ScrollTop = 0;
        ClipModel clip = view.Session.Clips.First(candidate => candidate.Name == "Rhythm guitar");
        double startX = view.ClipLeft(clip) + 60;
        double startY = (clip.Track * view.LaneHeight) + (view.LaneHeight / 2);
        int lastStep = -1;
        await RunFramesAsync(t =>
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
    }

    private async Task RunFramesAsync(Action<double> step)
    {
        TaskCompletionSource finished = new();
        double begin = -1;
        recorder.Start(timestamp =>
        {
            if (begin < 0)
            {
                begin = timestamp;
            }
            double elapsed = (timestamp - begin) / 1000;
            if (elapsed >= ScenarioSeconds)
            {
                finished.TrySetResult();
                return;
            }
            step(elapsed);
        });
        await finished.Task;
        FrameStats stats = recorder.Stop("load");
        _scenarios++;
        _frames += stats.Frames;
        _over100 += stats.FramesOver100Ms;
        _maxFrameMs = Math.Max(_maxFrameMs, stats.MaxMs);
        _worstSecond = Math.Min(_worstSecond, stats.WorstSecondFps);
    }
}
