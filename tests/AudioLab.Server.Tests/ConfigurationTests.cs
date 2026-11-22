using AudioLab.Server.Configuration;
using Microsoft.Extensions.Configuration;

namespace AudioLab.Server.Tests;

public sealed class ConfigurationTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("Swarm")]
    [InlineData("Hosted")]
    public void AProxyDoorWithoutASecretDoesNotStart(string kind)
    {
        Dictionary<string, string?> settings = new() { ["urls"] = "http://127.0.0.1:0", ["AudioLab:Door:Kind"] = kind };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ServerHost.Build([], settings));
        Assert.Contains("ProxySecret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATooShortSecretDoesNotStart()
    {
        Dictionary<string, string?> settings = new()
        {
            ["urls"] = "http://127.0.0.1:0",
            ["AudioLab:Door:Kind"] = "Swarm",
            ["AudioLab:Door:ProxySecret"] = "short",
        };
        Assert.Throws<InvalidOperationException>(() => ServerHost.Build([], settings));
    }

    [Theory]
    [InlineData("audiolab")]
    [InlineData("/audiolab/")]
    [InlineData("/a/../b")]
    [InlineData("/a//b")]
    [InlineData("/a?x=1")]
    [InlineData("/.hidden")]
    public void AMalformedPathBaseDoesNotStart(string pathBase)
    {
        Dictionary<string, string?> settings = new() { ["urls"] = "http://127.0.0.1:0", ["AudioLab:PathBase"] = pathBase };
        Assert.Throws<InvalidOperationException>(() => ServerHost.Build([], settings));
    }

    [Fact]
    public void AnUnknownDoorDoesNotStart()
    {
        Dictionary<string, string?> settings = new() { ["urls"] = "http://127.0.0.1:0", ["AudioLab:Door:Kind"] = "7" };
        Assert.Throws<InvalidOperationException>(() => ServerHost.Build([], settings));
    }

    [Theory]
    [InlineData("Desktop")]
    [InlineData("Swarm")]
    public void ALoopbackOnlyDoorDoesNotStartOnAnOutwardAddress(string kind)
    {
        Dictionary<string, string?> settings = new()
        {
            ["urls"] = "http://0.0.0.0:0",
            ["AudioLab:Door:Kind"] = kind,
            ["AudioLab:Door:ProxySecret"] = Secret,
        };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ServerHost.Build([], settings));
        Assert.Contains("loopback", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://127.0.0.1:5280", true)]
    [InlineData("http://localhost:5280", true)]
    [InlineData("http://LOCALHOST:5280", true)]
    [InlineData("https://[::1]:5280", true)]
    [InlineData("http://127.5.5.5:1", true)]
    [InlineData("http://0.0.0.0:5280", false)]
    [InlineData("http://*:5280", false)]
    [InlineData("http://+:5280", false)]
    [InlineData("http://[::]:5280", false)]
    [InlineData("http://192.168.1.4:5280", false)]
    [InlineData("http://example.com", false)]
    [InlineData("ftp://127.0.0.1", false)]
    [InlineData("", false)]
    public void ListenGuardKnowsLoopbackAddresses(string url, bool loopback)
    {
        Assert.Equal(loopback, ListenGuard.IsLoopbackUrl(url));
    }

    [Fact]
    public void ListenGuardFlagsPortsThatBindEveryInterfaceAndKestrelEndpoints()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["http_ports"] = "8080",
            ["Kestrel:Endpoints:Web:Url"] = "http://0.0.0.0:9000",
        }).Build();
        Assert.Equal(2, ListenGuard.Problems(DoorKind.Desktop, configuration).Count);
        Assert.Empty(ListenGuard.Problems(DoorKind.Hosted, configuration));
    }

    [Fact]
    public void TheHostedDoorMayListenOnAnyInterface()
    {
        Dictionary<string, string?> settings = new()
        {
            ["urls"] = "http://0.0.0.0:0",
            ["AudioLab:Door:Kind"] = "Hosted",
            ["AudioLab:Door:ProxySecret"] = Secret,
        };
        Assert.NotNull(ServerHost.Build([], settings));
    }
}
