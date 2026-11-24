using System.Globalization;
using AudioLab.Core.Commands;
using AudioLab.Core.Realtime;
using AudioLab.Web;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Media;
using AudioLab.Web.Services;
using AudioLab.Web.Session;
using AudioLab.Web.Session.Effects;
using AudioLab.Web.Session.Roll;
using AudioLab.Web.Waveforms;
using Bit.Bheadless;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// The browser hands the runtime the user's language, and a number written in German reads 6,5, which a style attribute drops. Everything
// the page writes for a machine (a length, a path) is written with the invariant culture, and so, here, is everything else the app formats.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
Uri site = new(builder.HostEnvironment.BaseAddress);
builder.Services.AddSingleton(new HttpClient { BaseAddress = site });
builder.Services.AddBitBheadlessServices();
if (OperatingSystem.IsBrowser())
{
    Shortcut.CommandKey = BrowserPlatform.IsApple() ? "Cmd" : "Ctrl";
    builder.Services.AddScoped<INativeOverlay, NativeOverlay>();
    builder.Services.AddSingleton<ITokenReader, TokenReader>();
    builder.Services.AddSingleton<IElementFocus, ElementFocus>();
    builder.Services.AddSingleton<IElementClick, ElementClick>();
    builder.Services.AddSingleton<IKeyGuard, KeyGuard>();
    builder.Services.AddSingleton<IPointerTracker, PointerTracker>();
    builder.Services.AddSingleton<IShortcutListener, ShortcutListener>();
    builder.Services.AddSingleton<IElementMetrics, ElementMetrics>();
    builder.Services.AddSingleton<IElementView, ElementView>();
    builder.Services.AddSingleton<IAudioHost, AudioHost>();
    builder.Services.AddSingleton<PlaybackService>();
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(CoreCommands.CreateRegistry());
builder.Services.AddSingleton<IProjectHubFactory>(new SignalRProjectHubFactory(new Uri(site, HubProtocol.Path.TrimStart('/'))));
builder.Services.AddSingleton<CommandSender>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<SessionClient>();
builder.Services.AddScoped<ProjectApi>();
// A large upload takes as long as it takes: its client has no timeout, and only the user stops it.
builder.Services.AddScoped(provider => new MediaApi(
    provider.GetRequiredService<HttpClient>(),
    new HttpClient { BaseAddress = site, Timeout = Timeout.InfiniteTimeSpan }));
builder.Services.AddScoped<MediaLibrary>();
builder.Services.AddScoped<MediaDragState>();
builder.Services.AddScoped<MediaAdder>();
builder.Services.AddScoped<TimelineZoom>();
builder.Services.AddScoped<MediaDragger>();
builder.Services.AddScoped<ProjectRenamer>();
builder.Services.AddScoped<SessionSelection>();
builder.Services.AddScoped<ClipDragState>();
builder.Services.AddScoped<BottomPanelState>();
builder.Services.AddScoped<EffectSelection>();
builder.Services.AddScoped<PianoRollState>();
builder.Services.AddScoped<RollDragState>();
builder.Services.AddScoped<PianoRollKeys>();
builder.Services.AddSingleton<IAuditionSink, UnavailableAudition>();
builder.Services.AddSingleton<IMidiTransfer, UnavailableMidiTransfer>();
builder.Services.AddScoped<NoteAudition>();
builder.Services.AddSingleton<LiveProjectFactory>();
builder.Services.AddScoped<PeakStore>();
builder.Services.AddScoped<WaveformService>();
builder.Services.AddSingleton(provider => new TrackColorMap(
    [.. Enumerable.Range(1, 12).Select(number => provider.GetRequiredService<ITokenReader>().Read($"--track-{number}"))]));
await builder.Build().RunAsync();
