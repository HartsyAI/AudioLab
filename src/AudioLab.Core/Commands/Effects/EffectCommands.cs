using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The effect commands: a track's insert chain, which runs before its fader, in list order.</summary>
internal static class EffectCommands
{
    public static void Register(CommandRegistryBuilder builder, EffectCommandJsonContext json)
    {
        builder
            .Add("effect_add", "Adds an effect at the end of a track's chain and allocates its id.", json.AddEffect, Add)
            .Add("effect_remove", "Removes an effect from a track's chain.", json.RemoveEffect, Remove)
            .Add("effect_move", "Moves an effect one step in the chain; at the end nothing changes.", json.MoveEffect, Move)
            .Add("effect_set_enabled", "Switches an effect on or bypasses it.", json.SetEffectEnabled, SetEnabled)
            .Add("effect_set_parameter", "Sets one numeric setting of an effect.", json.SetEffectParameter, SetParameter)
            .Add("effect_set_delay_sync", "Sets whether a delay follows the tempo, and its division.", json.SetDelaySync, SetDelaySync)
            .Add("effect_apply_preset", "Replaces an effect's settings with a preset's.", json.ApplyEffectPreset, ApplyPreset);
    }

    private static CommandResult Add(ProjectDocument document, AddEffect command, IIdService ids)
    {
        if (TrackEdits.Exists(document, command.Track) is { } missing)
        {
            return missing;
        }
        Track track = document.Tracks.First(candidate => candidate.Id == command.Track);
        EffectPreset? preset = null;
        if (command.Preset is not null && (preset = EffectPresets.Find(command.Kind, command.Preset)) is null)
        {
            return CommandChecks.OutOfRange($"The preset must be one of: {string.Join(", ", EffectPresets.For(command.Kind).Select(known => known.Name))}.");
        }
        if (track.Effects.Count >= ProjectLimits.MaxEffects)
        {
            return CommandChecks.OutOfRange($"A track has at most {ProjectLimits.MaxEffects} effects.");
        }
        Effect added = EffectRules.Create(command.Kind, ids.Next<EffectId>());
        if (preset is not null)
        {
            added = EffectPresets.Apply(added, preset);
        }
        int index = document.Tracks.ToList().FindIndex(candidate => candidate.Id == command.Track);
        return CommandResult.Ok(ClipEdits.WithTrack(document, index, current => current with { Effects = [.. current.Effects, added] }), added.Id.Value);
    }

    private static CommandResult Remove(ProjectDocument document, RemoveEffect command, IIdService ids)
    {
        return Edit(document, command.Track, command.Effect, effects => [.. effects.Where(effect => effect.Id != command.Effect)]);
    }

    private static CommandResult Move(ProjectDocument document, MoveEffect command, IIdService ids)
    {
        return Edit(document, command.Track, command.Effect, effects =>
        {
            List<Effect> list = [.. effects];
            int from = list.FindIndex(effect => effect.Id == command.Effect);
            int to = from + (command.Direction == MoveDirection.Down ? 1 : -1);
            if (to >= 0 && to < list.Count)
            {
                (list[from], list[to]) = (list[to], list[from]);
            }
            return new ValueList<Effect>(list);
        });
    }

    private static CommandResult SetEnabled(ProjectDocument document, SetEffectEnabled command, IIdService ids)
    {
        return Change(document, command.Track, command.Effect, effect => effect with { Enabled = command.Enabled });
    }

    private static CommandResult SetParameter(ProjectDocument document, SetEffectParameter command, IIdService ids)
    {
        if (Find(document, command.Track, command.Effect, out Effect? effect) is { } missing)
        {
            return missing;
        }
        EffectParameter? setting = EffectRules.Find(effect!, command.Parameter);
        if (setting is null)
        {
            return CommandChecks.Rejected($"A {EffectRules.KindOf(effect!).ToString().ToLowerInvariant()} has no setting named {Shown(command.Parameter)}.");
        }
        return CommandChecks.Range(command.Parameter, command.Value, setting.Min, setting.Max)
            ?? Change(document, command.Track, command.Effect, current => setting.Set(current, command.Value));
    }

    private static CommandResult SetDelaySync(ProjectDocument document, SetDelaySync command, IIdService ids)
    {
        if (Find(document, command.Track, command.Effect, out Effect? effect) is { } missing)
        {
            return missing;
        }
        if (effect is not DelayEffect)
        {
            return CommandChecks.Rejected("Only a delay follows the tempo.");
        }
        if (command.Division is not null && !EffectRules.IsDivision(command.Division))
        {
            return CommandChecks.OutOfRange($"The division must be one of: {string.Join(", ", EffectRules.Divisions)}.");
        }
        return Change(document, command.Track, command.Effect, current => ((DelayEffect)current) with { Sync = command.Sync, Division = command.Division ?? ((DelayEffect)current).Division });
    }

    private static CommandResult ApplyPreset(ProjectDocument document, ApplyEffectPreset command, IIdService ids)
    {
        if (Find(document, command.Track, command.Effect, out Effect? effect) is { } missing)
        {
            return missing;
        }
        EffectKind kind = EffectRules.KindOf(effect!);
        EffectPreset? preset = EffectPresets.Find(kind, command.Preset);
        return preset is null
            ? CommandChecks.Rejected($"There is no {kind.ToString().ToLowerInvariant()} preset named {Shown(command.Preset)}.")
            : Change(document, command.Track, command.Effect, current => EffectPresets.Apply(current, preset));
    }

    // A refusal when the track or the effect is missing; the effect comes out when it is there.
    private static CommandResult? Find(ProjectDocument document, TrackId trackId, EffectId effectId, out Effect? effect)
    {
        effect = null;
        if (TrackEdits.Exists(document, trackId) is { } missing)
        {
            return missing;
        }
        effect = document.Tracks.First(track => track.Id == trackId).Effects.FirstOrDefault(candidate => candidate.Id == effectId);
        return effect is null ? CommandChecks.NotFound("effect", effectId.Value) : null;
    }

    private static CommandResult Change(ProjectDocument document, TrackId trackId, EffectId effectId, Func<Effect, Effect> edit)
    {
        return Edit(document, trackId, effectId, effects => [.. effects.Select(effect => effect.Id == effectId ? edit(effect) : effect)]);
    }

    private static CommandResult Edit(ProjectDocument document, TrackId trackId, EffectId effectId, Func<ValueList<Effect>, ValueList<Effect>> edit)
    {
        return Find(document, trackId, effectId, out _) ?? TrackEdits.Replace(document, trackId, track => track with { Effects = edit(track.Effects) });
    }

    // A name from a client, cut to a readable length for a message.
    private static string Shown(string name) => ProjectLimits.Shorten(name.Length > 40 ? name[..40] : name);
}
