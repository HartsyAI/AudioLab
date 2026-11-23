using System.Net;
using System.Text.RegularExpressions;

namespace AudioLab.Server.Tests;

public sealed class EngineFileRouteTests
{
    private static bool Wasm(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".wasm", StringComparison.Ordinal);

    [Theory]
    [InlineData("/audio-shim.js")]
    [InlineData("/worker/_framework/dotnet.js")]
    public async Task TheEnginesScriptAndRuntimeAreServedWithoutADoorAndWithTheProgramsHeaders(string address)
    {
        await using RunningServer server = await RunningServer.StartAsync();

        using HttpResponseMessage response = await server.Client.GetAsync(address);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("SAMEORIGIN", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task TheWorkersBootConfigNamesTheEngineAssemblyThePageStartsAndEveryFileItNamesIsServed()
    {
        await using RunningServer server = await RunningServer.StartAsync();

        string loader = await server.Client.GetStringAsync("/worker/_framework/dotnet.js");
        string[] names =
        [
            .. Regex.Matches(loader, "\"name\": \"([^\"]+)\"").Select(match => match.Groups[1].Value).Distinct(),
        ];

        Assert.Contains(names, name => Wasm(name, "AudioLab.Audio.Worker."));
        Assert.Contains(names, name => Wasm(name, "AudioLab.Audio.") && !name.StartsWith("AudioLab.Audio.Worker.", StringComparison.Ordinal));
        foreach (string name in names)
        {
            using HttpResponseMessage file = await server.Client.GetAsync($"/worker/_framework/{name}");
            Assert.True(file.StatusCode == HttpStatusCode.OK, $"{name} answered {(int)file.StatusCode}");
            if (name.EndsWith(".wasm", StringComparison.Ordinal))
            {
                // Under nosniff a browser starts a module only when it is served as WebAssembly.
                Assert.Equal("application/wasm", file.Content.Headers.ContentType?.MediaType);
            }
        }
    }
}
