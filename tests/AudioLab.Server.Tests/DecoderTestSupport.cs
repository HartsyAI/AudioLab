using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AudioLab.Core.Api.Decoder;
using AudioLab.Server.Decoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Server.Tests;

/// <summary>Builds the fake decoder build the tests install, and the requests that drive the decoder routes and the media upload.</summary>
internal static class DecoderTestSupport
{
    public const string Version = "9.9.9";
    public const string EntryName = "fake-build/bin/ffmpeg.exe";
    private const string Secret = "0123456789abcdef0123456789abcdef";
    private const string SourceUrl = "https://fake.example.invalid/decoder.zip";

    public static readonly byte[] Binary = Encoding.UTF8.GetBytes("fake decoder binary, never run");

    public static byte[] Archive(byte[] binary)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry(EntryName);
            using Stream target = entry.Open();
            target.Write(binary);
        }
        return stream.ToArray();
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static DecoderBuild BuildFor(byte[] archive, byte[] binary) =>
        new(new DecoderPackage(Version, SourceUrl, archive.Length, Sha256(archive)), ["fake.example.invalid"], EntryName, "ffmpeg.exe", Sha256(binary));

    /// <summary>Starts the server with the fake build, downloader and launcher in place of the real ones.</summary>
    public static Task<RunningServer> StartAsync(
        DecoderBuild? build,
        FakeDecoderDownloader downloader,
        FakeDecoderLauncher launcher,
        IDictionary<string, string?>? settings = null)
    {
        Dictionary<string, string?> all = new()
        {
            ["AudioLab:Door:Kind"] = "Swarm",
            ["AudioLab:Door:ProxySecret"] = Secret,
        };
        foreach (KeyValuePair<string, string?> pair in settings ?? new Dictionary<string, string?>())
        {
            all[pair.Key] = pair.Value;
        }
        return RunningServer.StartAsync(all, services =>
        {
            // Registered after the server's own services, so these are the ones the routes and the upload resolve.
            services.AddSingleton<IDecoderLauncher>(launcher);
            services.AddSingleton<IDecoderDownloader>(downloader);
            services.AddSingleton<DecoderInstaller>(provider => new DecoderInstaller(
                provider.GetRequiredService<DecoderOptions>(),
                provider.GetRequiredService<DecoderLocation>(),
                downloader,
                build,
                NullLogger<DecoderInstaller>.Instance));
        });
    }

    /// <summary>The path the fake build's binary is installed at under the server's store.</summary>
    public static string BinaryPath(RunningServer server, DecoderBuild build) =>
        server.Services.GetRequiredService<DecoderLocation>().Binary(build.Package.Version, build.BinaryName);

    /// <summary>Puts a binary where a finished install would, so an upload can run the decoder without downloading anything.</summary>
    public static void PlaceBinary(RunningServer server, DecoderBuild build, byte[] binary)
    {
        string path = BinaryPath(server, build);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, binary);
    }

    public static HttpRequestMessage Request(HttpMethod method, string path, string user = "alice", HttpContent? content = null)
    {
        HttpRequestMessage request = new(method, path) { Content = content };
        request.Headers.Add("X-AudioLab-Secret", Secret);
        request.Headers.Add("X-AudioLab-User", user);
        return request;
    }

    public static async Task<HttpResponseMessage> PostInstallAsync(RunningServer server, string version, string sha256)
    {
        StringContent content = new($"{{\"version\":\"{version}\",\"sha256\":\"{sha256}\"}}", Encoding.UTF8, "application/json");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/decoder/install", content: content);
        return await server.Client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> UploadAsync(RunningServer server, byte[] file, string name, string type, string user = "alice")
    {
        ByteArrayContent content = new(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/media?name={Uri.EscapeDataString(name)}", user, content);
        return await server.Client.SendAsync(request);
    }

    /// <summary>Polls the decoder status until its phase is one of <paramref name="phases"/>, and returns that status.</summary>
    public static async Task<JsonElement> WaitForPhaseAsync(RunningServer server, params string[] phases)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            using HttpRequestMessage request = Request(HttpMethod.Get, "/api/decoder");
            using HttpResponseMessage response = await server.Client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            using JsonDocument document = JsonDocument.Parse(body);
            string? phase = document.RootElement.GetProperty("phase").GetString();
            if (phase is not null && phases.Contains(phase))
            {
                return document.RootElement.Clone();
            }
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"The install did not reach {string.Join(" or ", phases)}; its status was: {body}");
            }
            await Task.Delay(25);
        }
    }

    /// <summary>The temporary folder of the decoder, which must be empty once any request has finished.</summary>
    public static string[] TempFiles(RunningServer server) =>
        Directory.GetFiles(server.Services.GetRequiredService<DecoderLocation>().TempFolder);
}
