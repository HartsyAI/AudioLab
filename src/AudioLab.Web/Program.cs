using AudioLab.Core.Commands;
using AudioLab.Core.Realtime;
using AudioLab.Web;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
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
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(CoreCommands.CreateRegistry());
builder.Services.AddSingleton<IProjectHubFactory>(new SignalRProjectHubFactory(new Uri(site, HubProtocol.Path.TrimStart('/'))));
builder.Services.AddSingleton<CommandSender>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<SessionClient>();
builder.Services.AddScoped<ProjectApi>();
builder.Services.AddScoped<ProjectRenamer>();
await builder.Build().RunAsync();
