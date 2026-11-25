using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Web.Tests;

/// <summary>A disabled fader stays in the tab order, is not a disabled control, and says why in its title.</summary>
public sealed class FaderDisabledTests
{
    /// <summary>The disabled slider is a button that is marked aria-disabled, with no disabled attribute, and its title is the reason.</summary>
    [Fact]
    public async Task ADisabledFaderStaysFocusableAndSaysWhy()
    {
        RenderedMarkup fader = await RenderedMarkup.OfAsync<Fader>(
            new Dictionary<string, object?>
            {
                ["Label"] = "Volume of Kick",
                ["Text"] = "0.0 dB",
                ["Position"] = 0.8,
                ["OnCommit"] = EventCallback.Factory.Create<double>(new object(), _ => { }),
                ["Disabled"] = true,
                ["Reason"] = "Offline.",
            },
            services => services.AddSingleton<IPointerTracker>(new NoPointerTracker()));

        Assert.Contains("slider", fader.Values("role"));
        Assert.Contains("true", fader.Values("aria-disabled"));
        Assert.DoesNotContain(fader.Attributes, attribute => attribute.Name == "disabled");
        Assert.DoesNotContain(fader.Attributes, attribute => attribute.Name == "tabindex");
        Assert.Contains("Offline.", fader.Values("title"));
        await fader.DisposeServicesAsync();
    }
}
