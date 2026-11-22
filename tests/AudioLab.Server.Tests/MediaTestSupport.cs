using System.Globalization;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Realtime;

namespace AudioLab.Server.Tests;

/// <summary>Builds projects that play media, through the real commands, for the media tests.</summary>
internal static class MediaTestSupport
{
    public static readonly ClientId Client = ClientId.From("test-client");

    public static byte[] Wav(int frames = 24000) => TestWav.Build(SampleEncoding.Pcm16, 2, 48000, frames);

    public static async Task<ProjectId> NewProjectAsync(TestStore store, UserId user, string name = "Song")
    {
        return ProjectId.From((await store.Service.CreateAsync(user, name, CancellationToken.None)).Id);
    }

    /// <summary>Applies a command by wire name; <paramref name="members"/> is the rest of its JSON object, without braces.</summary>
    public static async Task<CommandReply> ApplyAsync(TestStore store, UserId user, ProjectId project, string name, string members = "")
    {
        string json = $"{{\"project\":\"{project.Value}\"" + (members.Length > 0 ? "," + members : string.Empty) + "}";
        return await store.Edits.ApplyAsync(user, project, Client, name, json, null, CancellationToken.None);
    }

    public static async Task<string> AddTrackAsync(TestStore store, UserId user, ProjectId project)
    {
        CommandReply reply = await ApplyAsync(store, user, project, "track_add", "\"kind\":\"audio\"");
        Assert.True(reply.Succeeded, string.Join("; ", reply.Errors.Select(error => error.Message)));
        return reply.Created.Single();
    }

    /// <summary>Puts a clip of the media on a new track and returns the clip's id.</summary>
    public static async Task<string> AddClipAsync(TestStore store, UserId user, ProjectId project, MediaRecord media, double? duration = null)
    {
        string track = await AddTrackAsync(store, user, project);
        string length = (duration ?? media.Duration).ToString("R", CultureInfo.InvariantCulture);
        string members = $"\"track\":\"{track}\",\"media\":\"{media.Id.Value}\",\"duration\":{length}";
        CommandReply reply = await ApplyAsync(store, user, project, "clip_add", members);
        Assert.True(reply.Succeeded, string.Join("; ", reply.Errors.Select(error => error.Message)));
        return reply.Created.Single();
    }

    public static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        await using (stream)
        {
            using MemoryStream copy = new();
            await stream.CopyToAsync(copy);
            return copy.ToArray();
        }
    }
}
