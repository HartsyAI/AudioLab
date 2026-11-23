using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AudioLab.Core.Api;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Realtime;
using AudioLab.Server.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AudioLab.Server.Tests;

/// <summary>What the export tests share: projects built through real commands, requests, and a WAV reader that is not the server's.</summary>
internal static class ExportTestSupport
{
    public const string Secret = "0123456789abcdef0123456789abcdef";

    public static readonly ClientId Client = ClientId.From("export-tests");

    public static Dictionary<string, string?> Swarm() => new() { ["AudioLab:Door:Kind"] = "Swarm", ["AudioLab:Door:ProxySecret"] = Secret };

    public static HttpRequestMessage Request(HttpMethod method, string path, string? user = null, HttpContent? content = null)
    {
        HttpRequestMessage request = new(method, path) { Content = content };
        if (user is not null)
        {
            request.Headers.Add("X-AudioLab-Secret", Secret);
            request.Headers.Add("X-AudioLab-User", user);
        }
        return request;
    }

    public static async Task<ProjectId> CreateProjectAsync(RunningServer server, string name = "Song", string? user = null)
    {
        JsonContent body = JsonContent.Create(new CreateProjectRequest(name), ApiJsonContext.Default.CreateProjectRequest);
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/projects", user, body);
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ProjectSummary summary = (await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.ProjectSummary))!;
        return ProjectId.From(summary.Id);
    }

    public static async Task<MediaSummary> UploadAsync(RunningServer server, byte[] file, string? user = null)
    {
        ByteArrayContent content = new(file);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using HttpRequestMessage request = Request(HttpMethod.Post, "/api/media?name=take.wav", user, content);
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync(MediaJsonContext.Default.MediaSummary))!;
    }

    public static async Task<CommandReply> ApplyAsync(RunningServer server, UserId user, ProjectId project, string name, string members = "")
    {
        ProjectEditService edits = server.Services.GetRequiredService<ProjectEditService>();
        string json = $"{{\"project\":\"{project.Value}\"" + (members.Length > 0 ? "," + members : string.Empty) + "}";
        CommandReply reply = await edits.ApplyAsync(user, project, Client, name, json, null, CancellationToken.None);
        Assert.True(reply.Succeeded, string.Join("; ", reply.Errors.Select(error => error.Message)));
        return reply;
    }

    /// <summary>Puts a clip of the whole file on a new track at fader unity, and returns the track's id.</summary>
    public static async Task<string> AddClipAsync(RunningServer server, UserId user, ProjectId project, MediaSummary media, string? trackName = null)
    {
        string track = (await ApplyAsync(server, user, project, "track_add", "\"kind\":\"audio\"")).Created.Single();
        string length = media.Duration.ToString("R", CultureInfo.InvariantCulture);
        _ = await ApplyAsync(server, user, project, "clip_add", $"\"track\":\"{track}\",\"media\":\"{media.Id}\",\"duration\":{length}");
        _ = await ApplyAsync(server, user, project, "mixer_set_track_volume", $"\"track\":\"{track}\",\"volume\":1");
        if (trackName is not null)
        {
            string quoted = System.Text.Json.JsonSerializer.Serialize(trackName);
            _ = await ApplyAsync(server, user, project, "track_rename", $"\"track\":\"{track}\",\"name\":{quoted}");
        }
        return track;
    }

    public static async Task<HttpResponseMessage> ExportAsync(
        RunningServer server,
        ProjectId project,
        string kind,
        string json = "{}",
        string? user = null,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        StringContent body = new(json, Encoding.UTF8, "application/json");
        using HttpRequestMessage request = Request(HttpMethod.Post, $"/api/projects/{project.Value}/export/{kind}", user, body);
        return await server.Client.SendAsync(request, completion);
    }

    public static async Task WaitForAsync(Func<bool> condition)
    {
        DateTime until = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "the condition did not come true in 20 seconds");
            await Task.Delay(20);
        }
    }

    /// <summary>A WAV file read by hand, so that what the server wrote is checked by code that is not the server's.</summary>
    public sealed record WavInfo(int Tag, int Channels, int Rate, int Bits, int Frames, byte[] Data)
    {
        public static WavInfo Parse(byte[] file)
        {
            Assert.Equal("RIFF", Encoding.ASCII.GetString(file, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(file, 8, 4));
            Assert.Equal(file.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(4)));
            int tag = 0;
            int channels = 0;
            int rate = 0;
            int bits = 0;
            int position = 12;
            while (position < file.Length)
            {
                string id = Encoding.ASCII.GetString(file, position, 4);
                int size = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(position + 4));
                if (id == "fmt ")
                {
                    tag = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(position + 8));
                    channels = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(position + 10));
                    rate = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(position + 12));
                    bits = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(position + 22));
                }
                else if (id == "data")
                {
                    byte[] data = file.AsSpan(position + 8, size).ToArray();
                    return new WavInfo(tag, channels, rate, bits, size / (channels * bits / 8), data);
                }
                position += 8 + size + (size & 1);
            }
            throw new InvalidDataException("no data chunk");
        }

        public double Sample(int frame, int channel)
        {
            int width = Bits / 8;
            ReadOnlySpan<byte> at = Data.AsSpan(((frame * Channels) + channel) * width, width);
            return Tag == 3
                ? BinaryPrimitives.ReadSingleLittleEndian(at)
                : Bits == 16
                    ? BinaryPrimitives.ReadInt16LittleEndian(at) / 32768.0
                    : (((at[0] | (at[1] << 8) | (at[2] << 16)) << 8) >> 8) / 8388608.0;
        }
    }
}
