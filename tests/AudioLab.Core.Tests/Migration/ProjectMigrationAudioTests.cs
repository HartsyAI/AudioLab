using System.Text.Json.Nodes;
using AudioLab.Core.Migration;
using AudioLab.Core.Model;
using static AudioLab.Core.Tests.TestJson;

namespace AudioLab.Core.Tests.Migration;

public sealed class ProjectMigrationAudioTests
{
    private static readonly UserId _owner = UserId.ForDoor("swarm", "7");

    [Fact]
    public void AServerSavedProjectWithItsAudioListIsReadAndEachFileIsDescribedForTheStore()
    {
        MigrationResult result = Migrate(V3Fixtures.Read(V3Fixtures.Full));
        Assert.Equal(
            [
                ("blob-1-100", "audio/wav", 1000L),
                ("blob-2-200", "audio/webm", 2000L),
                ("blob-9-900", "audio/wav", 300L),
            ],
            result.Media.Select(media => (media.BlobKey, media.ContentType, media.Length)));
        Assert.All(result.Media, media => Assert.Null(media.InlineBase64));
    }

    [Fact]
    public void AClipWhoseAudioIsNotListedIsDroppedAsTheExtensionDoesOnOpen()
    {
        MigrationResult result = Migrate(V3Fixtures.Read(V3Fixtures.Full));
        Assert.Equal(3, result.Document.Tracks[0].Clips.Count);
        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith("tracks[0].clips[4] was dropped", StringComparison.Ordinal)
                && warning.Contains("'blob-lost-500'"));
        Assert.DoesNotContain(result.Media, media => media.BlobKey == "blob-lost-500");
    }

    [Fact]
    public void ASamplerAndADrumLaneWhoseAudioIsNotListedKeepNoSampleAndSaySo()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root["audio"]!.AsObject().Remove("blob-9-900");
        root["audio"]!.AsObject().Remove("blob-1-100");
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Null(((SamplerInstrument)result.Document.Tracks[2].Instrument!).Sample);
        Assert.Null(result.Document.BeatPattern.Lanes[0].Media);
        Assert.Null(result.Document.BeatPattern.Lanes[1].Media);
        Assert.Contains(result.Warnings, warning => warning.Contains("tracks[2].instrument: its sample 'blob-1-100'"));
        Assert.Contains(result.Warnings, warning => warning.Contains("beatPattern.lanes[1]: its audio 'blob-9-900'"));
        Assert.Equal(["blob-2-200"], result.Media.Select(media => media.BlobKey));
    }

    [Fact]
    public void AnOldProjectThatCarriesItsAudioInsideTheJsonHandsTheBase64Over()
    {
        MigrationResult result = Migrate(V3Fixtures.Read(V3Fixtures.Inline));
        Assert.Equal(
            [("blob-1-1", "audio/wav", 3L, "AAEC"), ("blob-2-2", "audio/mpeg", 4L, "AAECAw==")],
            result.Media.Select(media => (media.BlobKey, media.ContentType, media.Length, media.InlineBase64)));
        Assert.Equal(2, result.Document.Tracks[0].Clips.Count);
        Assert.Equal(result.Media[0].Media, result.Document.Tracks[0].Clips[0].Media);
    }

    [Fact]
    public void InlineAudioThatIsNotBase64IsRefusedWithItsPath()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Inline));
        root["blobs"]!["blob-1-1"]!["b64"] = "not base64!";
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains("blobs.blob-1-1.b64:", error.Message);
    }

    [Fact]
    public void AnAutosaveHasNoAudioListSoTheCallerMustSayWhichAudioExists()
    {
        string json = V3Fixtures.Read(V3Fixtures.Autosave);
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(json));
        Assert.Contains("tracks[0].clips[0].blobKey:", error.Message);
        Assert.Contains("supply the audio files", error.Message);
        Dictionary<string, MediaFileInfo> supplied = new() { ["blob-7-70"] = new MediaFileInfo("audio/wav", 5000) };
        MigrationResult result = ProjectMigration.FromV3(json, _owner, new SequentialIdService(), supplied);
        MediaReference file = result.Media[0];
        Assert.Equal(("blob-7-70", "audio/wav", 5000L), (file.BlobKey, file.ContentType, file.Length));
        Assert.Equal("Untitled project", result.Document.Name);
    }

    [Fact]
    public void SuppliedAudioThatLacksAKeyDropsTheClipThatUsesIt()
    {
        Dictionary<string, MediaFileInfo> supplied = new() { ["blob-other"] = new MediaFileInfo("audio/wav", 10) };
        string json = V3Fixtures.Read(V3Fixtures.Autosave);
        MigrationResult result = ProjectMigration.FromV3(json, _owner, new SequentialIdService(), supplied);
        Assert.Empty(result.Document.Tracks[0].Clips);
        Assert.Empty(result.Media);
        Assert.Contains(result.Warnings, warning => warning.Contains("'blob-7-70'"));
    }

    [Fact]
    public void TheProjectsOwnListWinsOverWhatTheCallerSupplies()
    {
        Dictionary<string, MediaFileInfo> supplied = new() { ["blob-1-100"] = new MediaFileInfo("audio/ogg", 9) };
        string json = V3Fixtures.Read(V3Fixtures.Full);
        MigrationResult result = ProjectMigration.FromV3(json, _owner, new SequentialIdService(), supplied);
        Assert.Equal(("audio/wav", 1000L), (result.Media[0].ContentType, result.Media[0].Length));
    }

    [Fact]
    public void AnAudioEntryWithNoBytesCountsAsMissing()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root["audio"]!["blob-2-200"]!["size"] = 0;
        MigrationResult result = Migrate(root.ToJsonString());
        Assert.Equal(2, result.Document.Tracks[0].Clips.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("audio.blob-2-200 has no bytes"));
    }

    [Fact]
    public void AnAudioTypeThatIsNotAMediaTypeBecomesGenericBinaryAndSaysSo()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root["audio"]!["blob-9-900"]!["type"] = "not a type";
        MigrationResult result = Migrate(root.ToJsonString());
        MediaReference file = result.Media.Single(media => media.BlobKey == "blob-9-900");
        Assert.Equal("application/octet-stream", file.ContentType);
        Assert.Contains(result.Warnings, warning => warning.Contains("audio.blob-9-900 has the type 'not a type'"));
    }

    [Fact]
    public void AnAudioEntryWithAnUnknownFieldIsRefused()
    {
        JsonObject root = Parse(V3Fixtures.Read(V3Fixtures.Full));
        root["audio"]!["blob-9-900"]!["duration"] = 3;
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => Migrate(root.ToJsonString()));
        Assert.Contains("'duration'", error.Message);
    }

    [Fact]
    public void AProjectThatUsesNoAudioNeedsNoAudioList()
    {
        MigrationResult result = Migrate(V3Fixtures.Read(V3Fixtures.Minimal));
        Assert.Empty(result.Media);
    }

    private static MigrationResult Migrate(string json)
    {
        return ProjectMigration.FromV3(json, _owner, new SequentialIdService());
    }
}
