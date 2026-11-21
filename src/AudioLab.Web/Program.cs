using AudioLab.Web.Components;
using AudioLab.Web.Interop;
using AudioLab.Web.Pages;
using Bit.Bheadless;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<PrimitivesPage>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddBitBheadlessServices();
builder.Services.AddScoped<AlToastService>();
builder.Services.AddScoped<INativeOverlay, NativeOverlay>();
await builder.Build().RunAsync();
