using AudioLab.Web.Bench;
using AudioLab.Web.Interop;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>What every lane-drawing candidate shares: the view, the drag, and a repaint scheduled once per animation frame.</summary>
public abstract class LaneSurfaceBase : ComponentBase, IDisposable
{
    private bool _scheduled;

    /// <summary>Scroll, zoom and size.</summary>
    [Inject]
    protected TimelineView View { get; set; } = null!;

    /// <summary>The drag in progress.</summary>
    [Inject]
    protected DragController Drag { get; set; } = null!;

    /// <summary>The browser.</summary>
    [Inject]
    protected IBrowser Browser { get; set; } = null!;

    /// <summary>Frame measurement.</summary>
    [Inject]
    protected FrameRecorder Recorder { get; set; } = null!;

    /// <summary>The session shown.</summary>
    protected SessionModel Session => View.Session;

    /// <summary>Asks for a repaint in the next animation frame; any number of requests in one frame cost one repaint.</summary>
    protected void Invalidate()
    {
        if (!_scheduled)
        {
            _scheduled = true;
            Browser.RequestFrame(OnFrame);
        }
    }

    /// <summary>Draws the surface; called at most once per animation frame.</summary>
    protected abstract void Draw();

    /// <summary>Reports the cost of a draw to the recorder.</summary>
    protected void ReportDraw(double start, int calls, int clips) => Recorder.RecordDraw(Browser.Now() - start, calls, clips);

    /// <summary>Called when the scroll position or size changed.</summary>
    protected virtual void OnViewChanged() => Invalidate();

    /// <summary>Called when the drag changed.</summary>
    protected virtual void OnDragChanged(ClipModel? clip) => Invalidate();

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        View.Changed += OnViewChanged;
        Drag.Changed += OnDragChanged;
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        View.Changed -= OnViewChanged;
        Drag.Changed -= OnDragChanged;
    }

    private void OnFrame(double timestamp)
    {
        _scheduled = false;
        Draw();
    }
}
