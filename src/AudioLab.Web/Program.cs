using AudioLab.Web;
using AudioLab.Web.Bench;
using AudioLab.Web.Interop;
using AudioLab.Web.Playback;
using AudioLab.Web.Rendering;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

BrowserHost browser = new();
SpikeOptions options = SpikeOptions.FromSearch(browser.LocationSearch);
foreach (string face in new[] { "400 12px Inter", "500 12px Inter", "600 12px Inter" })
{
    await browser.LoadFontAsync(face);
}

TokenSet tokens = TokenSet.Read(browser);
SessionModel session = SyntheticSession.Create(options.Tracks, options.Clips, options.Bars);
TimelineView view = new(session, options.BarWidth, tokens.Length("--lane-height"), tokens.Length("--space-4"), tokens.Length("--space-16"));

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<IBrowser>(browser);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(tokens);
builder.Services.AddSingleton(session);
builder.Services.AddSingleton(view);
builder.Services.AddSingleton<DragController>();
builder.Services.AddSingleton<WavePathCache>();
builder.Services.AddSingleton<FrameRecorder>();
builder.Services.AddSingleton<LoadRunner>();
builder.Services.AddSingleton<IAudioBridge, AudioBridge>();
builder.Services.AddSingleton<PlaybackRun>();
await builder.Build().RunAsync();
