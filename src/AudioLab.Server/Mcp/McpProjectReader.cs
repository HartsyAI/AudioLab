using System.Text.Json.Nodes;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Server.Store;
using ModelContextProtocol.Protocol;

namespace AudioLab.Server.Mcp;

/// <summary>Reads a part of a project for get_project as the document's own JSON, so a result can stay small however large the project is.</summary>
public static class McpProjectReader
{
    private static readonly string[] _bulky = ["markers", "sections", "groups", "beatPattern", "tracks"];

    /// <summary>The part of <paramref name="snapshot"/> that <paramref name="arguments"/> asks for, or the refusal to return.</summary>
    public static CallToolResult Read(ProjectSnapshot snapshot, GetProjectArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Offset < 0 || arguments.Count < 0)
        {
            return Refuse(CommandErrorCode.Malformed, "offset and count cannot be negative.", snapshot.Revision);
        }
        JsonObject document = JsonNode.Parse(ProjectJson.Write(snapshot.Document))!.AsObject();
        long revision = snapshot.Revision;
        return (arguments.Section ?? ProjectSection.Summary) switch
        {
            ProjectSection.Summary => McpResults.Json(Summary(document, revision)),
            ProjectSection.Settings => McpResults.Json(Settings(document, revision)),
            ProjectSection.Markers => List(document["markers"], "markers", arguments, revision),
            ProjectSection.Sections => List(document["sections"], "sections", arguments, revision),
            ProjectSection.Groups => List(document["groups"], "groups", arguments, revision),
            ProjectSection.BeatPattern => BeatPattern(document, revision),
            ProjectSection.Tracks => Tracks(document, arguments, revision),
            ProjectSection.AudioClips => Clips(document, "clips", arguments, revision),
            ProjectSection.MidiClips => Clips(document, "midiClips", arguments, revision),
            ProjectSection.Notes => Notes(document, "notes", arguments, revision),
            ProjectSection.ControlChanges => Notes(document, "controlChanges", arguments, revision),
            _ => Refuse(CommandErrorCode.Malformed, "That is not a part of a project.", revision),
        };
    }

    private static JsonObject Summary(JsonObject document, long revision)
    {
        JsonArray tracks = [];
        foreach (JsonObject track in document["tracks"]!.AsArray().Select(node => node!.AsObject()))
        {
            tracks.Add(new JsonObject
            {
                ["id"] = track["id"]!.DeepClone(),
                ["name"] = track["name"]!.DeepClone(),
                ["kind"] = track["kind"]!.DeepClone(),
                ["clipCount"] = track["clips"]!.AsArray().Count,
                ["midiClipCount"] = track["midiClips"]!.AsArray().Count,
            });
        }
        return new JsonObject
        {
            ["revision"] = revision,
            ["name"] = document["name"]!.DeepClone(),
            ["bpm"] = document["bpm"]!.DeepClone(),
            ["timeSignature"] = document["timeSignature"]!.DeepClone(),
            ["tempoChangeCount"] = document["tempoMap"]!.AsArray().Count,
            ["markerCount"] = document["markers"]!.AsArray().Count,
            ["sectionCount"] = document["sections"]!.AsArray().Count,
            ["groupCount"] = document["groups"]!.AsArray().Count,
            ["trackCount"] = tracks.Count,
            ["tracks"] = tracks,
        };
    }

    private static CallToolResult BeatPattern(JsonObject document, long revision)
    {
        return McpResults.Json(new JsonObject { ["revision"] = revision, ["beatPattern"] = document["beatPattern"]!.DeepClone() });
    }

    private static JsonObject Settings(JsonObject document, long revision)
    {
        JsonObject settings = new() { ["revision"] = revision };
        foreach (KeyValuePair<string, JsonNode?> member in document.Where(member => !_bulky.Contains(member.Key)))
        {
            settings[member.Key] = member.Value?.DeepClone();
        }
        return settings;
    }

    private static CallToolResult Tracks(JsonObject document, GetProjectArguments arguments, long revision)
    {
        IEnumerable<JsonObject> tracks = document["tracks"]!.AsArray().Select(node => node!.AsObject());
        if (arguments.Track is { } wanted)
        {
            JsonObject? one = tracks.FirstOrDefault(track => Is(track, wanted.Value));
            if (one is null)
            {
                return Refuse(CommandErrorCode.NotFound, $"The project has no track {wanted.Value}.", revision);
            }
            tracks = [one];
        }
        JsonArray withoutClips = [];
        foreach (JsonObject track in tracks)
        {
            JsonObject copy = (JsonObject)track.DeepClone();
            copy["clipCount"] = copy["clips"]!.AsArray().Count;
            copy["midiClipCount"] = copy["midiClips"]!.AsArray().Count;
            copy.Remove("clips");
            copy.Remove("midiClips");
            withoutClips.Add(copy);
        }
        return List(withoutClips, "tracks", arguments, revision);
    }

    private static CallToolResult Clips(JsonObject document, string member, GetProjectArguments arguments, long revision)
    {
        string name = member == "clips" ? "audio_clips" : "midi_clips";
        if (!FindTrack(document, arguments, name, revision, out JsonObject? track, out CallToolResult? refusal))
        {
            return refusal!;
        }
        IEnumerable<JsonObject> clips = track![member]!.AsArray().Select(node => node!.AsObject());
        if (arguments.Clip is { } wanted)
        {
            clips = clips.Where(clip => Is(clip, wanted.Value));
        }
        JsonArray items = [];
        foreach (JsonObject clip in clips)
        {
            JsonObject copy = (JsonObject)clip.DeepClone();
            if (member == "midiClips")
            {
                copy["noteCount"] = copy["notes"]!.AsArray().Count;
                copy["controlChangeCount"] = copy["controlChanges"]!.AsArray().Count;
                copy.Remove("notes");
                copy.Remove("controlChanges");
            }
            items.Add(copy);
        }
        return List(items, name, arguments, revision);
    }

    private static CallToolResult Notes(JsonObject document, string member, GetProjectArguments arguments, long revision)
    {
        string name = member == "notes" ? "notes" : "control_changes";
        if (!FindTrack(document, arguments, name, revision, out JsonObject? track, out CallToolResult? refusal))
        {
            return refusal!;
        }
        if (arguments.Clip is not { } wanted)
        {
            return Refuse(CommandErrorCode.Malformed, $"Section {name} needs clip: the id of a MIDI clip of the track.", revision);
        }
        JsonObject? clip = track!["midiClips"]!.AsArray().Select(node => node!.AsObject()).FirstOrDefault(candidate => Is(candidate, wanted.Value));
        return clip is null
            ? Refuse(CommandErrorCode.NotFound, $"The track has no MIDI clip {wanted.Value}.", revision)
            : List(clip[member], name, arguments, revision);
    }

    private static bool FindTrack(
        JsonObject document,
        GetProjectArguments arguments,
        string section,
        long revision,
        out JsonObject? track,
        out CallToolResult? refusal)
    {
        track = null;
        refusal = null;
        if (arguments.Track is not { } wanted)
        {
            refusal = Refuse(CommandErrorCode.Malformed, $"Section {section} needs track: the id of a track, from the summary.", revision);
            return false;
        }
        track = document["tracks"]!.AsArray().Select(node => node!.AsObject()).FirstOrDefault(candidate => Is(candidate, wanted.Value));
        if (track is null)
        {
            refusal = Refuse(CommandErrorCode.NotFound, $"The project has no track {wanted.Value}.", revision);
        }
        return track is not null;
    }

    private static CallToolResult List(JsonNode? source, string section, GetProjectArguments arguments, long revision)
    {
        JsonNode?[] all = [.. source!.AsArray()];
        int offset = Math.Min(arguments.Offset ?? 0, all.Length);
        int count = Math.Min(arguments.Count ?? all.Length, all.Length - offset);
        JsonArray items = [.. all.Skip(offset).Take(count).Select(node => node?.DeepClone())];
        return McpResults.Json(new JsonObject
        {
            ["revision"] = revision,
            ["section"] = section,
            ["total"] = all.Length,
            ["offset"] = offset,
            ["items"] = items,
        });
    }

    private static bool Is(JsonObject item, string id) => item["id"]?.GetValue<string>() == id;

    private static CallToolResult Refuse(CommandErrorCode code, string message, long revision) => McpResults.Refused(code, message, revision);
}
