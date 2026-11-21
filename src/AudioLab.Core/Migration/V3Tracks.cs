using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>Tracks, clips, MIDI clips, automation and sends.</summary>
internal static class V3Tracks
{
    private const double MinMidiLength = 0.05;

    /// <summary>Gives every track and audio clip its new id first, so references that point forward still resolve.</summary>
    public static void Reserve(List<V3Track> raw, V3Context context)
    {
        for (int i = 0; i < raw.Count; i++)
        {
            string path = $"tracks[{i}]";
            string id = raw[i].Id ?? string.Empty;
            if (id.Length == 0)
            {
                throw V3Context.Fail($"{path}.id", "is missing; sends and groups could not tell tracks apart.");
            }
            if (!context.TrackIds.TryAdd(id, context.Ids.Next<TrackId>()))
            {
                throw V3Context.Fail($"{path}.id", $"the id {id} is used by two tracks, so sends to it are ambiguous.");
            }
            foreach (V3Clip clip in raw[i].Clips ?? [])
            {
                string clipId = clip.Id ?? string.Empty;
                if (clipId.Length == 0)
                {
                    throw V3Context.Fail($"{path}.clips", "a clip has no id.");
                }
                if (!context.ClipIds.TryAdd(clipId, context.Ids.Next<ClipId>()))
                {
                    context.Warn($"Two clips had the id {clipId}; links to it point at the first.");
                }
            }
        }
    }

    public static List<Track> Convert(List<V3Track> raw, Dictionary<string, GroupId> groupIds, V3Context context)
    {
        HashSet<string> claimed = new(StringComparer.Ordinal);
        List<Track> tracks = [];
        for (int i = 0; i < raw.Count; i++)
        {
            tracks.Add(ConvertTrack(raw[i], $"tracks[{i}]", groupIds, claimed, context));
        }
        return ConvertSends(raw, tracks, context);
    }

    private static Track ConvertTrack(
        V3Track raw,
        string path,
        Dictionary<string, GroupId> groupIds,
        HashSet<string> claimed,
        V3Context context)
    {
        TrackKind kind = raw.Kind switch
        {
            null or "audio" => TrackKind.Audio,
            "instrument" => TrackKind.Instrument,
            "bus" => TrackKind.Bus,
            _ => throw V3Context.Fail($"{path}.kind", $"'{raw.Kind}' is not audio, instrument or bus."),
        };
        List<V3Clip> clips = raw.Clips ?? [];
        return new Track
        {
            Id = context.TrackIds[raw.Id!],
            Name = string.IsNullOrWhiteSpace(raw.Name) ? "Track" : raw.Name,
            Kind = kind,
            Color = string.IsNullOrEmpty(raw.Color) ? "#4a9eff" : raw.Color,
            Volume = raw.Volume ?? 0.8,
            Pan = raw.Pan ?? 0,
            Muted = raw.Muted == true,
            Soloed = raw.Soloed == true,
            Armed = false,
            Monitor = false,
            Group = ConvertGroup(raw.GroupId, groupIds, path, context),
            Icon = ConvertIcon(raw.Icon, path, context),
            Input = ConvertInput(raw.Input, $"{path}.input"),
            TakeCount = raw.TakeCount ?? 0,
            Instrument = V3Sound.ConvertInstrument(raw.Instrument, $"{path}.instrument", context),
            Clips = [.. clips.Select((clip, index) => ConvertClip(clip, $"{path}.clips[{index}]", claimed, context))],
            MidiClips = [.. (raw.MidiClips ?? []).Select((clip, index) => ConvertMidiClip(clip, $"{path}.midiClips[{index}]", context))],
            Effects = V3Sound.ConvertEffects(raw.Fx ?? [], $"{path}.fx", context),
            Automation = ConvertAutomation(raw, path),
            Sends = [],
            View = ConvertView(raw, path),
        };
    }

    private static GroupId? ConvertGroup(string? oldId, Dictionary<string, GroupId> groupIds, string path, V3Context context)
    {
        if (string.IsNullOrEmpty(oldId))
        {
            return null;
        }
        if (groupIds.TryGetValue(oldId, out GroupId group))
        {
            return group;
        }
        context.Warn($"{path} named a group that does not exist; it was taken out of the group.");
        return null;
    }

    private static string? ConvertIcon(string? icon, string path, V3Context context)
    {
        if (string.IsNullOrEmpty(icon))
        {
            return null;
        }
        if (TrackIcon.IsValid(icon))
        {
            return icon;
        }
        context.Warn($"{path} had an icon that is not short text or a small png, jpeg or webp image; it was dropped.");
        return null;
    }

    private static TrackInput ConvertInput(V3Input? raw, string path)
    {
        string? device = string.IsNullOrEmpty(raw?.DeviceId) ? null : raw.DeviceId;
        if (raw?.Channels is not { ValueKind: not JsonValueKind.Null } channels)
        {
            return new TrackInput(device, InputChannelMode.Mono);
        }
        if (channels.ValueKind == JsonValueKind.String)
        {
            return channels.GetString() switch
            {
                "mono" => new TrackInput(device, InputChannelMode.Mono),
                "stereo" => new TrackInput(device, InputChannelMode.Stereo),
                string other => throw V3Context.Fail($"{path}.channels", $"'{other}' is not mono, stereo or an input number."),
                null => new TrackInput(device, InputChannelMode.Mono),
            };
        }
        if (channels.ValueKind == JsonValueKind.Number && channels.TryGetInt32(out int number) && number >= 1)
        {
            return new TrackInput(device, InputChannelMode.Single, number);
        }
        throw V3Context.Fail($"{path}.channels", "must be mono, stereo or a whole input number from 1.");
    }

    private static AudioClip ConvertClip(V3Clip raw, string path, HashSet<string> claimed, V3Context context)
    {
        if (string.IsNullOrEmpty(raw.BlobKey))
        {
            throw V3Context.Fail($"{path}.blobKey", "is missing, so the clip has no audio to refer to.");
        }
        ClipId id = claimed.Add(raw.Id!) ? context.ClipIds[raw.Id!] : context.Ids.Next<ClipId>();
        return new AudioClip
        {
            Id = id,
            Name = raw.Name ?? string.Empty,
            Media = context.MediaFor(raw.BlobKey),
            Start = raw.StartTime ?? 0,
            Duration = raw.Duration ?? 0,
            Offset = raw.Offset ?? 0,
            TrimEnd = raw.TrimEnd ?? 0,
            Gain = raw.Gain ?? 1,
            FadeIn = raw.FadeIn ?? 0,
            FadeOut = raw.FadeOut ?? 0,
            Muted = raw.Muted == true,
            Color = string.IsNullOrEmpty(raw.Color) ? null : raw.Color,
            Meta = RemapMeta(raw.Meta, path, context),
        };
    }

    // A score's parent and a stem's source are clip ids, so they follow the clips to their new ids.
    private static ClipMeta? RemapMeta(ClipMeta? meta, string path, V3Context context)
    {
        if (meta is null)
        {
            return null;
        }
        ScoreMeta? score = meta.Score;
        if (score?.Parent is { } parent)
        {
            score = score with { Parent = Remap(parent, $"{path}.meta.score.parent", context) };
        }
        StemOf? stem = meta.StemOf;
        if (stem is { ClipId: { } source } known)
        {
            stem = known with { ClipId = Remap(source, $"{path}.meta.stemOf.clipId", context) };
        }
        return meta with { Score = score, StemOf = stem };
    }

    private static ClipId? Remap(ClipId old, string path, V3Context context)
    {
        if (context.ClipIds.TryGetValue(old.Value, out ClipId mapped))
        {
            return mapped;
        }
        context.Warn($"{path} pointed at a clip that is not in the project; the link was cleared.");
        return null;
    }

    private static MidiClip ConvertMidiClip(V3MidiClip raw, string path, V3Context context)
    {
        List<MidiNote> notes = [];
        NoteId next = NoteId.First;
        List<V3Note> rawNotes = raw.Notes ?? [];
        for (int i = 0; i < rawNotes.Count; i++)
        {
            V3Note note = rawNotes[i];
            string notePath = $"{path}.notes[{i}]";
            notes.Add(new MidiNote(
                next,
                note.Pitch ?? throw V3Context.Fail($"{notePath}.pitch", "is missing."),
                note.Start ?? throw V3Context.Fail($"{notePath}.start", "is missing."),
                note.Dur ?? throw V3Context.Fail($"{notePath}.dur", "is missing."),
                note.Vel ?? 100));
            next = next.Next();
        }
        List<MidiControlChange> changes = [];
        List<V3Cc> rawChanges = raw.Cc ?? [];
        for (int i = 0; i < rawChanges.Count; i++)
        {
            V3Cc change = rawChanges[i];
            string changePath = $"{path}.cc[{i}]";
            changes.Add(new MidiControlChange(
                change.T ?? throw V3Context.Fail($"{changePath}.t", "is missing."),
                change.N ?? throw V3Context.Fail($"{changePath}.n", "is missing."),
                change.V ?? throw V3Context.Fail($"{changePath}.v", "is missing.")));
        }
        return new MidiClip
        {
            Id = context.Ids.Next<ClipId>(),
            Name = raw.Name ?? string.Empty,
            Start = raw.StartTime ?? 0,
            Length = Math.Max(MinMidiLength, raw.Length ?? 0),
            Notes = notes,
            ControlChanges = [.. changes.OrderBy(change => change.Time)],
            Muted = raw.Muted == true,
            Color = string.IsNullOrEmpty(raw.Color) ? null : raw.Color,
        };
    }

    private static TrackAutomation ConvertAutomation(V3Track raw, string path)
    {
        AutomationMode mode = raw.AutomationMode switch
        {
            null or "read" => AutomationMode.Read,
            "touch" => AutomationMode.Touch,
            "latch" => AutomationMode.Latch,
            "write" => AutomationMode.Write,
            "off" => AutomationMode.Off,
            _ => throw V3Context.Fail($"{path}.automationMode", $"'{raw.AutomationMode}' is not a known automation mode."),
        };
        return new TrackAutomation
        {
            Mode = mode,
            Volume = ConvertPoints(raw.Automation?.Volume, $"{path}.automation.volume"),
            Pan = ConvertPoints(raw.Automation?.Pan, $"{path}.automation.pan"),
        };
    }

    private static List<AutomationPoint> ConvertPoints(List<V3Point>? raw, string path)
    {
        List<AutomationPoint> points = [];
        List<V3Point> source = raw ?? [];
        for (int i = 0; i < source.Count; i++)
        {
            points.Add(new AutomationPoint(
                source[i].T ?? throw V3Context.Fail($"{path}[{i}].t", "is missing."),
                source[i].V ?? throw V3Context.Fail($"{path}[{i}].v", "is missing.")));
        }
        return [.. points.OrderBy(point => point.Time)];
    }

    private static TrackView ConvertView(V3Track raw, string path)
    {
        AutomationParameter parameter = raw.AutomationParam switch
        {
            null or "volume" => AutomationParameter.Volume,
            "pan" => AutomationParameter.Pan,
            _ => throw V3Context.Fail($"{path}.automationParam", $"'{raw.AutomationParam}' is not volume or pan."),
        };
        return new TrackView
        {
            Height = raw.Height is { } height && height != 0 ? height : 100,
            AutomationVisible = raw.AutomationVisible == true,
            AutomationParameter = parameter,
        };
    }

    // A send to a bus the project does not have is dropped, and its bus is the track's new id.
    private static List<Track> ConvertSends(List<V3Track> raw, List<Track> tracks, V3Context context)
    {
        Dictionary<TrackId, TrackKind> kinds = tracks.ToDictionary(track => track.Id, track => track.Kind);
        List<Track> result = [];
        for (int i = 0; i < tracks.Count; i++)
        {
            List<Send> sends = [];
            HashSet<TrackId> targets = [];
            foreach (V3Send send in raw[i].Sends ?? [])
            {
                string? reason = DropReason(send, tracks[i], kinds, targets, context);
                if (reason is null)
                {
                    TrackId bus = context.TrackIds[send.BusId!];
                    targets.Add(bus);
                    sends.Add(new Send(bus, send.Level ?? 0.5, send.Pre == true, send.Muted == true));
                }
                else
                {
                    context.Warn($"tracks[{i}] had a send that was dropped: {reason}.");
                }
            }
            result.Add(tracks[i] with { Sends = sends });
        }
        return result;
    }

    private static string? DropReason(
        V3Send send,
        Track source,
        Dictionary<TrackId, TrackKind> kinds,
        HashSet<TrackId> targets,
        V3Context context)
    {
        if (string.IsNullOrEmpty(send.BusId) || !context.TrackIds.TryGetValue(send.BusId, out TrackId bus))
        {
            return "its bus is not in the project";
        }
        if (kinds[bus] != TrackKind.Bus)
        {
            return "its target is not a bus";
        }
        if (source.Kind == TrackKind.Bus)
        {
            return "a bus cannot send";
        }
        return targets.Contains(bus) ? "the track already sends to that bus" : null;
    }
}
