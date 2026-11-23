using AudioLab.Web;
using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
if (OperatingSystem.IsBrowser())
{
    builder.Services.AddScoped<INativeOverlay, NativeOverlay>();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<SessionClient>();
await builder.Build().RunAsync();
