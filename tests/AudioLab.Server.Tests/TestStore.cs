using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Configuration;
using AudioLab.Server.Media;
using AudioLab.Server.Services;
using AudioLab.Server.Store;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Server.Tests;

/// <summary>The whole store stack on a folder, with a clock a test moves and a write observer a test can set.</summary>
public sealed class TestStore : IAsyncDisposable
{
    private TestStore(
        string folder,
        StoreOptions options,
        CommandRegistry? registry,
        IProjectEvents? events,
        MediaOptions mediaOptions,
        Func<Task>? beforeRebuild,
        Func<Task>? betweenPasses)
    {
        Folder = folder;
        Options = options with { Root = folder };
        Time = new ManualTimeProvider();
        AtomicFile atomic = new(null, (stage, temp) => Observer?.Invoke(stage, temp));
        Root = new StoreRoot(Options, atomic, NullLogger<StoreRoot>.Instance);
        Projects = new ProjectStore(Root, NullLogger<ProjectStore>.Instance);
        Ids = new IdService();
        Registry = registry ?? new CommandRegistryBuilder().Build();
        Media = new MediaStore(Root, new OptionsMediaQuota(mediaOptions), mediaOptions, Time, NullLogger<MediaStore>.Instance);
        Media.SweepOrphans();
        Open = new OpenProjects(Projects, Options, Time, Registry, Media, Ids, NullLogger<OpenProjects>.Instance);
        MediaReferences references = new(Open, Projects, NullLogger<MediaReferences>.Instance, betweenPasses);
        Rebuilder = new PeakRebuilder(NullLogger<PeakRebuilder>.Instance, beforeRebuild);
        OptionsMediaQuota quota = new(mediaOptions);
        MediaFiles = new MediaService(Media, references, Root, Ids, mediaOptions, Time, Rebuilder, quota, NullLogger<MediaService>.Instance);
        Service = new ProjectService(Projects, Open, Ids, Time, events ?? NullProjectEvents.Instance);
        Edits = new ProjectEditService(Open, Registry, events ?? NullProjectEvents.Instance, new AudioLabOptions(), Media);
    }

    public string Folder { get; }

    public StoreOptions Options { get; }

    public ManualTimeProvider Time { get; }

    public StoreRoot Root { get; }

    public ProjectStore Projects { get; }

    public IIdService Ids { get; }

    public MediaStore Media { get; }

    public MediaService MediaFiles { get; }

    public PeakRebuilder Rebuilder { get; }

    public OpenProjects Open { get; }

    public ProjectService Service { get; }

    public ProjectEditService Edits { get; }

    public CommandRegistry Registry { get; }

    public Action<AtomicWriteStage, string>? Observer { get; set; }

    public StorePaths Paths => Root.Paths;

    public static TestStore On(
        string folder,
        StoreOptions? options = null,
        CommandRegistry? registry = null,
        IProjectEvents? events = null,
        MediaOptions? media = null,
        Func<Task>? beforeRebuild = null,
        Func<Task>? betweenPasses = null)
    {
        return new TestStore(folder, options ?? new StoreOptions(), registry, events, media ?? new MediaOptions(), beforeRebuild, betweenPasses);
    }

    /// <summary>Uploads a file as a user would, through the service, with the body arriving as a stream.</summary>
    public Task<MediaRecord> UploadAsync(UserId user, byte[] file, string name = "kick.wav")
    {
        return MediaFiles.UploadAsync(user, name, new MemoryStream(file), file.Length, CancellationToken.None);
    }

    /// <summary>The user's file as the command pipeline sees it: the media folder is read first, as the server does before it asks.</summary>
    public async Task<MediaRecord?> FindAsync(UserId user, MediaId id)
    {
        await Media.PrepareAsync(user);
        return Media.Find(user, id);
    }

    public string ProjectFile(UserId user, string projectId) => Paths.ProjectFile(user, ProjectId.From(projectId));

    /// <summary>Stops without writing anything, as a killed process does; only the store lock is released.</summary>
    public void Crash() => Root.Dispose();

    public async ValueTask DisposeAsync()
    {
        Observer = null;
        await Open.DisposeAsync();
        Root.Dispose();
    }
}
