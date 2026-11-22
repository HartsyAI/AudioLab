using AudioLab.Core.Commands;
using AudioLab.Core.Realtime;
using AudioLab.Web;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
Uri site = new(builder.HostEnvironment.BaseAddress);
builder.Services.AddSingleton(new HttpClient { BaseAddress = site });
if (OperatingSystem.IsBrowser())
{
    builder.Services.AddScoped<INativeOverlay, NativeOverlay>();
    builder.Services.AddSingleton<ITokenReader, TokenReader>();
    builder.Services.AddSingleton<IElementFocus, ElementFocus>();
    builder.Services.AddSingleton<IKeyGuard, KeyGuard>();
    builder.Services.AddSingleton<IPointerTracker, PointerTracker>();
    builder.Services.AddSingleton<IShortcutListener, ShortcutListener>();
    builder.Services.AddSingleton<IElementMetrics, ElementMetrics>();
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(CoreCommands.CreateRegistry());
builder.Services.AddSingleton<IProjectHubFactory>(new SignalRProjectHubFactory(new Uri(site, HubProtocol.Path.TrimStart('/'))));
builder.Services.AddSingleton<CommandSender>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<SessionClient>();
builder.Services.AddScoped<ProjectApi>();
builder.Services.AddScoped<ProjectRenamer>();
builder.Services.AddScoped<SessionSelection>();
builder.Services.AddScoped<ClipDragState>();
builder.Services.AddSingleton<LiveProjectFactory>();
builder.Services.AddSingleton<WavePathCache>();
builder.Services.AddSingleton(provider => new TrackColorMap(
    [.. Enumerable.Range(1, 12).Select(number => provider.GetRequiredService<ITokenReader>().Read($"--track-{number}"))]));
await builder.Build().RunAsync();
