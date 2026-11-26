using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>A drag that the fader leaves behind when it is taken down sends nothing, however far the pointer moves afterwards.</summary>
public sealed class FaderDragTests
{
    /// <summary>A press followed by a take-down: the pointer is let go, so moves and a release after it send nothing.</summary>
    [Fact]
    public async Task AFaderTakenDownDuringAPressSendsNothingWhenThePointerMovesOn()
    {
        DrivenPointer pointer = new();
        List<double> sent = [];
        RenderedMarkup fader = await RenderedMarkup.StartAsync<Fader>(
            new Dictionary<string, object?>
            {
                ["Label"] = "Volume of Kick",
                ["Text"] = "0.0 dB",
                ["Position"] = 0.5,
                ["OnCommit"] = EventCallback.Factory.Create<double>(new object(), value => sent.Add(value)),
            },
            services => services.AddSingleton<IPointerTracker>(pointer));

        await fader.RaiseAsync("onpointerdown", new PointerEventArgs { Button = 0, ClientY = 300 });
        fader.TakeDown();
        pointer.Move(0, 260);
        pointer.Release(0, 240);
        await Task.Delay(50);

        Assert.Empty(sent);
        await fader.DisposeServicesAsync();
    }

    private sealed class DrivenPointer : IPointerTracker
    {
        private Action<double, double>? _move;

        private Action<double, double>? _end;

        public bool Shift => false;

        public void Begin(Action<double, double> move, Action<double, double> end, Action cancel)
        {
            (_move, _end) = (move, end);
        }

        public void Stop()
        {
            (_move, _end) = (null, null);
        }

        public void Move(double x, double y) => _move?.Invoke(x, y);

        public void Release(double x, double y) => _end?.Invoke(x, y);
    }
}
