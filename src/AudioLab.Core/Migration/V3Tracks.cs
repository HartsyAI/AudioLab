using System.Text.Json;
using AudioLab.Core.Model;

namespace AudioLab.Core.Migration;

/// <summary>Tracks, clips, automation and sends.</summary>
internal static class V3Tracks
{
    /// <summary>Gives every track and every clip with audio its new id first, so forward references resolve.</summary>
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
            List<V3Clip> clips = V3Context.Items(raw[i].Clips, $"{path}.clips");
            for (int j = 0; j < clips.Count; j++)
            {
                string clipId = clips[j].Id ?? string.Empty;
                bool playable = MissingAudio(clips[j], $"{path}.clips[{j}]", context) is null;
                if (clipId.Length > 0 && playable && !context.ClipIds.TryAdd(clipId, context.Ids.Next<ClipId>()))
                {
                    context.Warn($"{path}.clips[{j}] repeats the id {clipId}; links to it point at the first.");
                }
            }
        }
    }

    /// <summary>Converts every track, then re-points sends; a send to a bus the project lacks is dropped.</summary>
    public static List<Track> Convert(List<V3Track> raw, Dictionary<string, GroupId> groupIds, V3Context context)
    {
        HashSet<string> claimed = new(StringComparer.Ordinal);
        List<Track> tracks = [];
        for (int i = 0; i < raw.Count; i++)
        {
            tracks.Add(ConvertTrack(raw[i], i, groupIds, claimed, context));
        }
        return ConvertSends(raw, tracks, context);
    }

    /// <summary>Clears a score parent that leads back to its own clip, directly or through other clips.</summary>
    public static List<Track> BreakScoreLinkLoops(List<Track> tracks, V3Context context)
    {
        Dictionary<ClipId, ClipId> parents = [];
        foreach (AudioClip clip in tracks.SelectMany(track => track.Clips))
        {
            if (clip.Meta?.Score?.Parent is { } parent)
            {
                parents[clip.Id] = parent;
            }
        }
        HashSet<ClipId> cleared = [];
        Dictionary<ClipId, bool> settled = [];
        foreach (ClipId start in parents.Keys)
        {
            List<ClipId> path = [];
            ClipId? current = start;
            while (current is { } clip && !settled.ContainsKey(clip))
            {
                settled[clip] = false;
                path.Add(clip);
                current = parents.TryGetValue(clip, out ClipId next) ? next : null;
            }
            if (current is { } hit && !settled[hit] && path.Contains(hit))
            {
                cleared.Add(path[^1]);
            }
            path.ForEach(visited => settled[visited] = true);
        }
        return cleared.Count == 0 ? tracks : [.. tracks.Select(track => ClearLinks(track, cleared, context))];
    }

    // The reason a clip has no audio to play, or null. The extension skips such a clip when it opens a project.
    private static string? MissingAudio(V3Clip clip, string path, V3Context context)
    {
        if (string.IsNullOrEmpty(clip.BlobKey))
        {
            return "it has no audio key";
        }
        return context.IsAvailable(clip.BlobKey, $"{path}.blobKey")
            ? null
            : $"its audio '{clip.BlobKey}' is not among the project's audio files";
    }

    private static Track ConvertTrack(
        V3Track raw,
        int index,
        Dictionary<string, GroupId> groupIds,
        HashSet<string> claimed,
        V3Context context)
    {
        string path = $"tracks[{index}]";
        TrackKind kind = raw.Kind switch
        {
            null or "audio" => TrackKind.Audio,
            "instrument" => TrackKind.Instrument,
            "bus" => TrackKind.Bus,
            _ => throw V3Context.Fail($"{path}.kind", $"'{raw.Kind}' is not audio, instrument or bus."),
        };
        return new Track
        {
            Id = context.TrackIds[raw.Id!],
            Name = string.IsNullOrWhiteSpace(raw.Name) ? $"Track {index + 1}" : raw.Name,
            Kind = kind,
            Color = context.Color(raw.Color, ProjectDefaults.TrackColor(index), $"{path}.color")!,
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
            Clips = [.. ConvertClips(V3Context.Items(raw.Clips, $"{path}.clips"), path, claimed, context)],
            MidiClips = [.. ConvertMidiClips(V3Context.Items(raw.MidiClips, $"{path}.midiClips"), path, context)],
            Effects = [.. V3Sound.ConvertEffects(V3Context.Items(raw.Fx, $"{path}.fx"), $"{path}.fx", context)],
            Automation = ConvertAutomation(raw, path, context),
            Sends = [],
            View = ConvertView(raw, path),
        };
    }

    private static GroupId? ConvertGroup(
        string? oldId,
        Dictionary<string, GroupId> groupIds,
        string path,
        V3Context context)
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
                string other => throw V3Context.Fail($"{path}.channels", $"'{other}' is not mono, stereo or a number."),
                null => new TrackInput(device, InputChannelMode.Mono),
            };
        }
        if (channels.ValueKind == JsonValueKind.Number && channels.TryGetInt32(out int number) && number >= 1)
        {
            return new TrackInput(device, InputChannelMode.Single, number);
        }
        throw V3Context.Fail($"{path}.channels", "must be mono, stereo or a whole input number from 1.");
    }

    private static List<AudioClip> ConvertClips(
        List<V3Clip> raw,
        string trackPath,
        HashSet<string> claimed,
        V3Context context)
    {
        List<AudioClip> clips = [];
        for (int i = 0; i < raw.Count; i++)
        {
            string path = $"{trackPath}.clips[{i}]";
            if (MissingAudio(raw[i], path, context) is { } reason)
            {
                context.Warn($"{path} was dropped, as the extension does on open: {reason}.");
                continue;
            }
            clips.Add(ConvertClip(raw[i], path, claimed, context));
        }
        return clips;
    }

    private static AudioClip ConvertClip(V3Clip raw, string path, HashSet<string> claimed, V3Context context)
    {
        bool keepsId = !string.IsNullOrEmpty(raw.Id) && claimed.Add(raw.Id);
        ClipId id = keepsId ? context.ClipIds[raw.Id!] : context.Ids.Next<ClipId>();
        double playable = (raw.Duration ?? 0) - (raw.Offset ?? 0) - (raw.TrimEnd ?? 0);
        return new AudioClip
        {
            Id = id,
            Name = raw.Name ?? string.Empty,
            Media = context.MediaFor(raw.BlobKey!, $"{path}.blobKey")!.Value,
            Start = raw.StartTime ?? 0,
            Duration = raw.Duration ?? 0,
            Offset = raw.Offset ?? 0,
            TrimEnd = raw.TrimEnd ?? 0,
            Gain = raw.Gain ?? 1,
            FadeIn = FitFade(raw.FadeIn ?? 0, playable, $"{path}.fadeIn", context),
            FadeOut = FitFade(raw.FadeOut ?? 0, playable, $"{path}.fadeOut", context),
            Muted = raw.Muted == true,
            Color = context.Color(raw.Color, null, $"{path}.color"),
            Meta = RemapMeta(raw.Meta, path, context),
        };
    }

    // The extension stores a fade as it was typed and clamps it only when it renders, so the stored one may be too long.
    private static double FitFade(double fade, double playable, string path, V3Context context)
    {
        if (playable <= 0 || fade <= playable)
        {
            return fade;
        }
        context.Warn($"{path} was {V3Context.Num(fade)} seconds, longer than the {V3Context.Num(playable)} that play; it was shortened.");
        return playable;
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

    private static List<MidiClip> ConvertMidiClips(List<V3MidiClip> raw, string trackPath, V3Context context)
    {
        return [.. raw.Select((clip, index) => V3Midi.ConvertClip(clip, $"{trackPath}.midiClips[{index}]", context))];
    }

    private static Track ClearLinks(Track track, HashSet<ClipId> cleared, V3Context context)
    {
        AudioClip[] clips = [.. track.Clips.Select(clip => cleared.Contains(clip.Id) ? Cleared(clip, context) : clip)];
        return track with { Clips = [.. clips] };
    }

    private static AudioClip Cleared(AudioClip clip, V3Context context)
    {
        context.Warn($"The score parents of the clip '{clip.Name}' lead back to it; its parent link was cleared.");
        ClipMeta meta = clip.Meta!;
        return clip with { Meta = meta with { Score = meta.Score! with { Parent = null } } };
    }

    private static TrackAutomation ConvertAutomation(V3Track raw, string path, V3Context context)
    {
        AutomationMode mode = raw.AutomationMode switch
        {
            null or "read" => AutomationMode.Read,
            "touch" => AutomationMode.Touch,
            "latch" => AutomationMode.Latch,
            "write" => AutomationMode.Write,
            "off" => AutomationMode.Off,
            _ => throw V3Context.Fail($"{path}.automationMode", $"'{raw.AutomationMode}' is not an automation mode."),
        };
        return new TrackAutomation
        {
            Mode = mode,
            Volume = [.. V3Midi.ConvertPoints(raw.Automation?.Volume, $"{path}.automation.volume", context)],
            Pan = [.. V3Midi.ConvertPoints(raw.Automation?.Pan, $"{path}.automation.pan", context)],
        };
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

    private static List<Track> ConvertSends(List<V3Track> raw, List<Track> tracks, V3Context context)
    {
        Dictionary<TrackId, TrackKind> kinds = tracks.ToDictionary(track => track.Id, track => track.Kind);
        List<Track> result = [];
        for (int i = 0; i < tracks.Count; i++)
        {
            List<V3Send> rawSends = V3Context.Items(raw[i].Sends, $"tracks[{i}].sends");
            List<Send> sends = [];
            HashSet<TrackId> targets = [];
            for (int j = 0; j < rawSends.Count; j++)
            {
                string? reason = DropReason(rawSends[j], tracks[i], kinds, targets, context);
                if (reason is null)
                {
                    TrackId bus = context.TrackIds[rawSends[j].BusId!];
                    targets.Add(bus);
                    V3Send send = rawSends[j];
                    sends.Add(new Send(bus, send.Level ?? 0.5, send.Pre == true, send.Muted == true));
                }
                else
                {
                    context.Warn($"tracks[{i}].sends[{j}] was dropped: {reason}.");
                }
            }
            result.Add(tracks[i] with { Sends = [.. sends] });
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
