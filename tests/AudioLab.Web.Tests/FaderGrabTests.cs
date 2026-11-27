using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The fader's grab: a press or the Space key grabs it, its moves are reported, and a release, a Space key up, a blur or a cancel ends it (D86).</summary>
/// <remarks>Each test fails when its wire is taken out of <see cref="Fader"/>: the grab, the drag or the let-go is missing from what the recorder receives.</remarks>
public sealed class FaderGrabTests
{
    /// <summary>A press grabs at the fader's place, each move reports its place, and the release reports the place it is let go at, with the value sent.</summary>
    [Fact]
    public async Task APressGrabsDragsAndLetsGoAtTheFaderPlace()
    {
        CancellablePointer pointer = new();
        List<double> grabs = [];
        List<double> drags = [];
        List<double> letGos = [];
        List<double> sent = [];
        RenderedMarkup fader = await Start(pointer, grabs, drags, letGos, sent);

        await fader.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        pointer.Move(0, 260);
        pointer.Release(0, 240);
        await Task.Delay(50);

        Assert.Equal([0.5], grabs);
        Assert.NotEmpty(drags);
        Assert.Single(letGos);
        Assert.Equal(letGos, sent);
        Assert.True(letGos[0] > 0.5);
        await fader.DisposeServicesAsync();
    }

    /// <summary>Space grabs the fader once; each arrow key while it is held reports the step; Space going up lets the grab go.</summary>
    [Fact]
    public async Task TheSpaceKeyGrabsAndLetsGoWhileTheArrowKeysStep()
    {
        CancellablePointer pointer = new();
        List<double> grabs = [];
        List<double> drags = [];
        List<double> letGos = [];
        List<double> sent = [];
        RenderedMarkup fader = await Start(pointer, grabs, drags, letGos, sent);

        await fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        await fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " ", Repeat = true });
        await fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await fader.RaiseAsync("onkeyup", new KeyboardEventArgs { Key = " " });
        await Task.Delay(50);

        Assert.Equal([0.5], grabs);
        Assert.Single(drags);
        Assert.True(drags[0] > 0.5);
        Assert.Equal([drags[0]], letGos);
        await fader.DisposeServicesAsync();
    }

    /// <summary>A fader that loses focus while Space is held lets its grab go, so no segment is left open.</summary>
    [Fact]
    public async Task ALostFocusLetsAHeldKeyboardGrabGo()
    {
        CancellablePointer pointer = new();
        List<double> grabs = [];
        List<double> drags = [];
        List<double> letGos = [];
        List<double> sent = [];
        RenderedMarkup fader = await Start(pointer, grabs, drags, letGos, sent);

        await fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        await fader.RaiseAsync("onblur", new FocusEventArgs());
        await Task.Delay(50);

        Assert.Equal([0.5], grabs);
        Assert.Equal([0.5], letGos);
        await fader.DisposeServicesAsync();
    }

    /// <summary>A pointer taken down during a grab reports a cancel and no release, so the recording it fed can be dropped.</summary>
    [Fact]
    public async Task ATakenDownGrabReportsACancelAndNoRelease()
    {
        CancellablePointer pointer = new();
        List<double> grabs = [];
        List<double> drags = [];
        List<double> letGos = [];
        List<double> sent = [];
        int cancels = 0;
        RenderedMarkup fader = await Start(pointer, grabs, drags, letGos, sent, () => cancels++);

        await fader.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        pointer.Cancel();
        await Task.Delay(50);

        Assert.Equal([0.5], grabs);
        Assert.Empty(letGos);
        Assert.Equal(1, cancels);
        await fader.DisposeServicesAsync();
    }

    /// <summary>A held grab steps from the place it holds, so a step after the drawn place has fallen back to the gain still moves on from the grab.</summary>
    [Fact]
    public async Task AHeldGrabStepsFromItsOwnPlaceAfterTheSendSettles()
    {
        CancellablePointer pointer = new();
        List<double> grabs = [];
        List<double> drags = [];
        List<double> letGos = [];
        List<double> sent = [];
        RenderedMarkup fader = await Start(pointer, grabs, drags, letGos, sent);

        await fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        await fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await Task.Delay(450);
        await fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await fader.RaiseAsync("onkeyup", new KeyboardEventArgs { Key = " " });
        await Task.Delay(50);

        Assert.Equal(2, drags.Count);
        Assert.True(drags[1] > drags[0]);
        await fader.DisposeServicesAsync();
    }

    /// <summary>A fader taken away while its grab is held cancels it, so the pass it fed is not left open.</summary>
    [Fact]
    public async Task ADisposedHeldGrabCancels()
    {
        CancellablePointer pointer = new();
        List<double> grabs = [];
        List<double> drags = [];
        List<double> letGos = [];
        List<double> sent = [];
        int cancels = 0;
        RenderedMarkup fader = await Start(pointer, grabs, drags, letGos, sent, () => cancels++);

        await fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        fader.TakeDown();
        await Task.Delay(50);

        Assert.Equal(1, cancels);
        Assert.Empty(letGos);
    }

    private static Task<RenderedMarkup> Start(
        CancellablePointer pointer,
        List<double> grabs,
        List<double> drags,
        List<double> letGos,
        List<double> sent,
        Action? cancelled = null)
    {
        return RenderedMarkup.StartAsync<Fader>(
            new Dictionary<string, object?>
            {
                ["Label"] = "Volume of Kick",
                ["Text"] = "0.0 dB",
                ["Position"] = 0.5,
                ["OnCommit"] = EventCallback.Factory.Create<double>(new object(), value => sent.Add(value)),
                ["OnGrab"] = EventCallback.Factory.Create<double>(new object(), place => grabs.Add(place)),
                ["OnDrag"] = EventCallback.Factory.Create<double>(new object(), place => drags.Add(place)),
                ["OnLetGo"] = EventCallback.Factory.Create<double>(new object(), place => letGos.Add(place)),
                ["OnCancel"] = EventCallback.Factory.Create(new object(), () => cancelled?.Invoke()),
            },
            services => services.AddSingleton<IPointerTracker>(pointer));
    }

    private sealed class CancellablePointer : IPointerTracker
    {
        private Action<double, double>? _move;

        private Action<double, double>? _end;

        private Action? _cancel;

        public bool Shift => false;

        public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
        {
            (_move, _end, _cancel) = (move, end, cancel);
        }

        public void Stop()
        {
            (_move, _end, _cancel) = (null, null, null);
        }

        public void Move(double x, double y) => _move?.Invoke(x, y);

        public void Release(double x, double y) => _end?.Invoke(x, y);

        public void Cancel() => _cancel?.Invoke();
    }
}
