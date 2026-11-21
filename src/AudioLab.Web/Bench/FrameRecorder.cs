using AudioLab.Web.Interop;

namespace AudioLab.Web.Bench;

/// <summary>Records the interval between animation frames and what each drawn frame cost, while a scenario runs.</summary>
public sealed class FrameRecorder(IBrowser browser)
{
    private readonly List<double> _stamps = [];

    private readonly List<double> _draws = [];

    private double _calls;

    private double _clips;

    private bool _running;

    private Action<double>? _onFrame;

    /// <summary>Whether a recording is in progress.</summary>
    public bool Running => _running;

    /// <summary>Starts recording; the callback runs in every animation frame before the stamp is taken, to drive a scenario.</summary>
    public void Start(Action<double>? onFrame)
    {
        _stamps.Clear();
        _draws.Clear();
        _calls = 0;
        _clips = 0;
        _onFrame = onFrame;
        _running = true;
        browser.RequestFrame(OnFrame);
    }

    /// <summary>Forgets what was recorded so far, to leave the warm-up out of the statistics.</summary>
    public void Clear()
    {
        _stamps.Clear();
        _draws.Clear();
        _calls = 0;
        _clips = 0;
    }

    /// <summary>Adds the cost of one drawn frame; a surface calls this for every frame it draws while recording.</summary>
    public void RecordDraw(double milliseconds, int calls, int clips)
    {
        if (_running)
        {
            _draws.Add(milliseconds);
            _calls += calls;
            _clips += clips;
        }
    }

    /// <summary>Stops recording and returns the statistics.</summary>
    public FrameStats Stop(string scenario)
    {
        _running = false;
        List<double> intervals = [];
        for (int i = 1; i < _stamps.Count; i++)
        {
            intervals.Add(_stamps[i] - _stamps[i - 1]);
        }
        double span = _stamps.Count > 1 ? _stamps[^1] - _stamps[0] : 1;
        double[] sorted = [.. intervals.Order()];
        double[] drawSorted = [.. _draws.Order()];
        int drawn = Math.Max(1, _draws.Count);
        return new FrameStats
        {
            Scenario = scenario,
            Frames = intervals.Count,
            MeanFps = Finite(intervals.Count / span * 1000),
            MedianMs = Finite(Percentile(sorted, 0.5)),
            P95Ms = Finite(Percentile(sorted, 0.95)),
            P99Ms = Finite(Percentile(sorted, 0.99)),
            MaxMs = sorted.Length > 0 ? sorted[^1] : 0,
            FramesOver45FpsLine = intervals.Count(interval => interval > 22.2),
            WorstSecondFps = Finite(WorstSecond()),
            DrawMeanMs = Finite(_draws.Count > 0 ? _draws.Average() : 0),
            DrawP95Ms = Finite(Percentile(drawSorted, 0.95)),
            DrawMaxMs = drawSorted.Length > 0 ? drawSorted[^1] : 0,
            CallsPerFrame = Finite(_calls / drawn),
            ClipsPerFrame = Finite(_clips / drawn),
        };
    }

    private static double Finite(double value) => double.IsFinite(value) ? value : -1;

    private static double Percentile(double[] sorted, double fraction)
    {
        return sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)(fraction * sorted.Length))];
    }

    private double WorstSecond()
    {
        double worst = double.MaxValue;
        int start = 0;
        for (int end = 1; end < _stamps.Count; end++)
        {
            while (_stamps[end] - _stamps[start] >= 1000)
            {
                start++;
            }
            if (_stamps[end] - _stamps[0] >= 1000)
            {
                worst = Math.Min(worst, end - start + 1);
            }
        }
        return worst == double.MaxValue ? 0 : worst;
    }

    private void OnFrame(double timestamp)
    {
        if (!_running)
        {
            return;
        }
        _onFrame?.Invoke(timestamp);
        _stamps.Add(timestamp);
        browser.RequestFrame(OnFrame);
    }
}
