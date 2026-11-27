using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>The fader's grab: a press or the Space key grabs it, its moves are reported, and a release, a Space key up, a blur or a cancel ends it (D86).</summary>
/// <remarks>Each test fails when its wire is taken out of <see cref="Fader"/>: the grab, the drag or the let-go is missing from what the recorder receives.
/// The handlers run on the renderer in order, so a barrier on the renderer ends each step; no test waits on a clock.</remarks>
public sealed class FaderGrabTests
{
    /// <summary>A press grabs at the fader's place, each move reports its place, and the release reports the place it is let go at, with the value sent.</summary>
    [Fact]
    public async Task APressGrabsDragsAndLetsGoAtTheFaderPlace()
    {
        Probe probe = await Probe.StartAsync();

        await probe.Fader.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        probe.Pointer.Move(0, 260);
        probe.Pointer.Release(0, 240);
        await probe.Barrier();

        Assert.Equal([0.5], probe.Grabs);
        Assert.NotEmpty(probe.Drags);
        Assert.Single(probe.LetGos);
        Assert.True(probe.LetGos[0].Sends);
        Assert.Equal(probe.LetGos.Select(release => release.Place), probe.Sent);
        Assert.True(probe.LetGos[0].Place > 0.5);
        await probe.DisposeAsync();
    }

    /// <summary>Space grabs the fader once; each arrow key while it is held reports the step; Space going up lets the grab go, with no send of its own.</summary>
    [Fact]
    public async Task TheSpaceKeyGrabsAndLetsGoWhileTheArrowKeysStep()
    {
        Probe probe = await Probe.StartAsync();

        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " ", Repeat = true });
        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await probe.Fader.RaiseAsync("onkeyup", new KeyboardEventArgs { Key = " " });

        Assert.Equal([0.5], probe.Grabs);
        Assert.Single(probe.Drags);
        Assert.True(probe.Drags[0] > 0.5);
        Assert.Equal([(probe.Drags[0], false)], probe.LetGos);
        await probe.DisposeAsync();
    }

    /// <summary>While the keyboard holds a grab, only Up and Down step it; Left, Right and Home leave the fader and the grab alone.</summary>
    [Fact]
    public async Task WhileHeldOnlyTheUpAndDownKeysStep()
    {
        Probe probe = await Probe.StartAsync();

        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });
        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "Home" });

        Assert.Empty(probe.Drags);
        Assert.Empty(probe.Sent);
        await probe.DisposeAsync();
    }

    /// <summary>A pointer press cannot start a second grab while the keyboard holds one: the recorder would refuse it silently.</summary>
    [Fact]
    public async Task APressWhileTheKeyboardHoldsAGrabStartsNothing()
    {
        Probe probe = await Probe.StartAsync();

        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        await probe.Fader.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        probe.Pointer.Release(0, 240);
        await probe.Barrier();

        Assert.Equal([0.5], probe.Grabs);
        Assert.Empty(probe.Sent);
        await probe.DisposeAsync();
    }

    /// <summary>A fader that loses focus while Space is held lets its grab go, so no segment is left open.</summary>
    [Fact]
    public async Task ALostFocusLetsAHeldKeyboardGrabGo()
    {
        Probe probe = await Probe.StartAsync();

        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        await probe.Fader.RaiseAsync("onblur", new FocusEventArgs());

        Assert.Equal([0.5], probe.Grabs);
        Assert.Equal([(0.5, false)], probe.LetGos);
        await probe.DisposeAsync();
    }

    /// <summary>A pointer taken down during a grab reports a cancel and no release, so the recording it fed can be dropped.</summary>
    [Fact]
    public async Task ATakenDownGrabReportsACancelAndNoRelease()
    {
        Probe probe = await Probe.StartAsync();

        await probe.Fader.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        probe.Pointer.Cancel();
        await probe.Barrier();

        Assert.Equal([0.5], probe.Grabs);
        Assert.Empty(probe.LetGos);
        Assert.Equal(1, probe.Cancels);
        await probe.DisposeAsync();
    }

    /// <summary>A held grab steps from the place it holds, so a step after the drawn place has fallen back to the gain still moves on from the grab.</summary>
    [Fact]
    public async Task AHeldGrabStepsFromItsOwnPlaceAfterTheSendSettles()
    {
        // A settle of zero makes the send's settle finish before the next key: the drawn travel is back at the setting by then.
        Probe probe = await Probe.StartAsync(TimeSpan.Zero);

        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await probe.Fader.RaiseAsync("onkeyup", new KeyboardEventArgs { Key = " " });

        Assert.Equal(2, probe.Drags.Count);
        Assert.True(probe.Drags[1] > probe.Drags[0]);
        await probe.DisposeAsync();
    }

    /// <summary>A fader taken away while its grab is held cancels it, so the pass it fed is not left open.</summary>
    [Fact]
    public async Task ADisposedHeldGrabCancels()
    {
        Probe probe = await Probe.StartAsync();

        await probe.Fader.RaiseAsync("onkeydown", new KeyboardEventArgs { Key = " " });
        probe.Fader.TakeDown();

        Assert.Equal(1, probe.Cancels);
        Assert.Empty(probe.LetGos);
        await probe.DisposeAsync();
    }

    // The fader under test, with the callbacks it raises written into lists, so a test reads what the recorder would have received.
    private sealed class Probe
    {
        private Probe(RenderedMarkup fader, DrivenPointer pointer)
        {
            Fader = fader;
            Pointer = pointer;
        }

        public RenderedMarkup Fader { get; }

        public DrivenPointer Pointer { get; }

        public List<double> Grabs { get; } = [];

        public List<double> Drags { get; } = [];

        public List<(double Place, bool Sends)> LetGos { get; } = [];

        public List<double> Sent { get; } = [];

        public int Cancels { get; private set; }

        public static async Task<Probe> StartAsync(TimeSpan? settle = null)
        {
            DrivenPointer pointer = new();
            Probe probe = null!;
            Dictionary<string, object?> parameters = new()
            {
                ["Label"] = "Volume of Kick",
                ["Text"] = "0.0 dB",
                ["Position"] = 0.5,
                ["OnCommit"] = EventCallback.Factory.Create<double>(new object(), value => probe.Sent.Add(value)),
                ["OnGrab"] = EventCallback.Factory.Create<double>(new object(), place => probe.Grabs.Add(place)),
                ["OnDrag"] = EventCallback.Factory.Create<double>(new object(), place => probe.Drags.Add(place)),
                ["OnLetGo"] = EventCallback.Factory.Create<(double Place, bool Sends)>(new object(), release => probe.LetGos.Add(release)),
                ["OnCancel"] = EventCallback.Factory.Create(new object(), () => probe.Cancels++),
            };
            if (settle is { } delay)
            {
                parameters["SettleDelay"] = delay;
            }
            RenderedMarkup fader = await RenderedMarkup.StartAsync<Fader>(parameters, services => services.AddSingleton<IPointerTracker>(pointer));
            probe = new Probe(fader, pointer);
            return probe;
        }

        // A barrier on the renderer: the handlers a pointer or key started have run once it returns.
        public Task Barrier() => Fader.RunAsync(() => { });

        public async Task DisposeAsync() => await Fader.DisposeServicesAsync();
    }

    private sealed class DrivenPointer : IPointerTracker
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
