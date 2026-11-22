using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using O9.Wasm;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Home>("#app");
await builder.Build().RunAsync();
