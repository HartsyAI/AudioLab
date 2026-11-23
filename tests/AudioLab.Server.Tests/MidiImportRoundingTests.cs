using System.Text.Json;
using AudioLab.Audio.Midi;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Services;

namespace AudioLab.Server.Tests;

// A file whose last note ends where the file ends makes a clip whose length is the note's start plus its duration, and the command
// asks that the note start 0.02 s before the end: (start + duration) - duration is not always the start in floating point.
public sealed class MidiImportRoundingTests
{
    private static readonly ClientId _client = ClientId.From("rounding-tests");

    private static byte[] Variable(long value)
    {
        List<byte> bytes = [(byte)(value & 0x7F)];
        for (value >>= 7; value > 0; value >>= 7)
        {
            bytes.Add((byte)((value & 0x7F) | 0x80));
        }
        bytes.Reverse();
        return [.. bytes];
    }

    // Format 0, one track: an optional tempo, a note from tick on to tick off, an optional pedal inside it, and the end of the track at off.
    private static byte[] File(int ticksPerQuarter, long on, long off, int microsecondsPerQuarter, bool pedal)
    {
        List<byte> track = [];
        track.AddRange(Variable(0));
        track.AddRange([0xFF, 0x51, 0x03, (byte)(microsecondsPerQuarter >> 16), (byte)(microsecondsPerQuarter >> 8), (byte)microsecondsPerQuarter]);
        track.AddRange(Variable(on));
        track.AddRange([0x90, 60, 100]);
        long at = on;
        if (pedal && off - on > 1)
        {
            track.AddRange(Variable(1));
            track.AddRange([0xB0, 64, 127]);
            at++;
        }
        track.AddRange(Variable(off - at));
        track.AddRange([0x80, 60, 0]);
        track.AddRange(Variable(0));
        track.AddRange([0xFF, 0x2F, 0x00]);
        List<byte> file = [.. "MThd"u8.ToArray()];
        file.AddRange([0, 0, 0, 6, 0, 0, 0, 1, (byte)(ticksPerQuarter >> 8), (byte)ticksPerQuarter]);
        file.AddRange("MTrk"u8.ToArray());
        file.AddRange([(byte)(track.Count >> 24), (byte)(track.Count >> 16), (byte)(track.Count >> 8), (byte)track.Count]);
        file.AddRange(track);
        return [.. file];
    }

    private static (CommandHistory History, TrackId Track) NewProject()
    {
        IdService ids = new();
        CommandRegistry registry = CoreCommands.CreateRegistry();
        ProjectDocument document = NewProjectFactory.Create(ids.Next<ProjectId>(), UserId.Local, "Rounding");
        CommandHistory history = new(document, registry, ids);
        string json = $"{{\"project\":\"{document.Id.Value}\",\"kind\":\"instrument\"}}";
        CommandResult added = history.Apply(registry.Parse("track_add", json).Command!, _client);
        Assert.True(added.Succeeded);
        return (history, TrackId.From(added.Created.Single()));
    }

    // The command the server builds from a file, as MidiService does.
    private static ImportMidiClip CommandOf(MidiImport import, ProjectId project, TrackId track) => new()
    {
        Project = project,
        Track = track,
        Start = 0,
        Length = import.Length,
        Notes = [.. import.Notes.Select(note => new ImportedNote(note.Pitch, note.Start, note.Duration, note.Velocity))],
        ControlChanges = [.. import.ControlChanges.Select(change => new ImportedControlChange(change.Time, change.Controller, change.Value))],
    };

    private static void AssertImports(int ticksPerQuarter, long on, long off, int microsecondsPerQuarter, bool pedal)
    {
        MidiImport import = MidiFile.Read(File(ticksPerQuarter, on, off, microsecondsPerQuarter, pedal));
        (CommandHistory history, TrackId track) = NewProject();

        CommandResult result = history.Apply(CommandOf(import, history.Current.Id, track), _client);

        Assert.True(
            result.Succeeded,
            $"{ticksPerQuarter} ticks, {on} to {off}, {microsecondsPerQuarter} us: "
            + string.Join("; ", result.Errors.Select(error => error.Message)));
    }

    [Fact]
    public void TheFileThatTheReviewFoundIsImported()
    {
        // 480 ticks a quarter note at 120 BPM: a note of 5 ticks from tick 61440, to the end of the track.
        AssertImports(480, 61440, 61445, 500_000, pedal: false);
    }

    [Fact]
    public void NoValidFileIsRefusedForTheFloatingPointOfItsOwnTimes()
    {
        Random random = new(20261123);
        int[] resolutions = [24, 48, 96, 120, 192, 240, 384, 480, 960, 1024];
        for (int round = 0; round < 4000; round++)
        {
            int ticks = resolutions[random.Next(resolutions.Length)];
            long on = random.NextInt64(0, 200_000);
            long length = random.Next(3) == 0 ? random.NextInt64(1, 6) : random.NextInt64(1, 3000);
            int tempo = random.Next(60_000, 1_500_000);
            AssertImports(ticks, on, on + length, tempo, pedal: random.Next(2) == 0);
        }
    }

    [Fact]
    public void EveryStartTickUnderTwoHundredThousandAtTheCommonResolutionsIsImportedWithTheShortestNote()
    {
        foreach (int ticks in new[] { 96, 480 })
        {
            for (long on = 0; on < 200_000; on += 1)
            {
                MidiImport import = MidiFile.Read(File(ticks, on, on + 1, 500_000, pedal: false));
                Assert.True(
                    NoteRulesAccept(import),
                    $"{ticks} ticks a quarter, a note of one tick from {on}: start {import.Notes[0].Start}, length {import.Length}");
            }
        }
    }

    // Only the rule that the review found: the command checks every note and event against it, and nothing else varies here.
    private static bool NoteRulesAccept(MidiImport import)
    {
        return import.Notes.All(note => AudioLab.Core.Rules.NoteRules.StartsInside(note.Start, import.Length) && note.Start < import.Length);
    }

    [Fact]
    public void TheCommandsSchemaDescribesAndBoundsTheNotesAndEventsItCarries()
    {
        CommandRegistry registry = CoreCommands.CreateRegistry();
        JsonElement schema = registry.Commands.Single(command => command.Name == "midi_clip_import").Schema;
        JsonElement properties = schema.GetProperty("properties");

        JsonElement note = properties.GetProperty("notes").GetProperty("items").GetProperty("properties");
        JsonElement control = properties.GetProperty("controlChanges").GetProperty("items").GetProperty("properties");

        Assert.All(new[] { "pitch", "start", "duration", "velocity" }, name => AssertDescribed(note.GetProperty(name)));
        Assert.All(new[] { "time", "controller", "value" }, name => AssertDescribed(control.GetProperty(name)));
        Assert.Equal((0, 127), Bounds(note.GetProperty("pitch")));
        Assert.Equal((1, 127), Bounds(note.GetProperty("velocity")));
        Assert.Equal(0.02, note.GetProperty("duration").GetProperty("minimum").GetDouble());
        Assert.Equal((0, 127), Bounds(control.GetProperty("controller")));
        Assert.Equal((0, 127), Bounds(control.GetProperty("value")));
        Assert.Equal(1_000_000, properties.GetProperty("notes").GetProperty("maxItems").GetInt32());
        Assert.Contains("hub and MCP", properties.GetProperty("notes").GetProperty("description").GetString(), StringComparison.Ordinal);
        string[] required = [.. schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal)];
        Assert.Equal(["controlChanges", "length", "notes", "project", "start", "track"], required);
    }

    private static void AssertDescribed(JsonElement property)
    {
        Assert.False(string.IsNullOrWhiteSpace(property.GetProperty("description").GetString()));
    }

    private static (int Minimum, int Maximum) Bounds(JsonElement property)
    {
        return (property.GetProperty("minimum").GetInt32(), property.GetProperty("maximum").GetInt32());
    }
}
