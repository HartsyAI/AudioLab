using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Commands;

/// <summary>The score and stem commands: what a clip remembers of the render that made it and of the clip it was separated from.</summary>
internal static class ClipMetaCommands
{
    public static void Register(CommandRegistryBuilder builder, ClipMetaCommandJsonContext json)
    {
        builder
            .Add("score_set", "Sets the score record of an audio clip.", json.SetClipScore, SetScore)
            .Add("score_set_label", "Sets the version label of a clip's score.", json.SetScoreLabel, SetLabel)
            .Add("score_clear", "Removes the score record of an audio clip.", json.ClearClipScore, ClearScore)
            .Add("stem_set", "Marks an audio clip as a stem of another.", json.SetClipStem, SetStem)
            .Add("stem_clear", "Removes the stem link of an audio clip.", json.ClearClipStem, ClearStem);
    }

    private static CommandResult SetScore(ProjectDocument document, SetClipScore command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        if (command.Parent is { } parent && Parent(document, clip, parent) is { } badParent)
        {
            return badParent;
        }
        CommandResult? invalid = CommandChecks.Length(nameof(command.Abc), command.Abc, ProjectLimits.MaxLongText)
            ?? CommandChecks.Length(nameof(command.Style), command.Style, ProjectLimits.MaxLongText)
            ?? CommandChecks.Length(nameof(command.Lyrics), command.Lyrics, ProjectLimits.MaxLongText)
            ?? CommandChecks.Length(nameof(command.Cot), command.Cot, ProjectLimits.MaxLabel)
            ?? CommandChecks.Length(nameof(command.Model), command.Model, ProjectLimits.MaxLabel)
            ?? CommandChecks.Length(nameof(command.EngineId), command.EngineId, ProjectLimits.MaxLabel)
            ?? CommandChecks.Length(nameof(command.Label), command.Label, ProjectLimits.MaxName)
            ?? CommandChecks.Length(nameof(command.Source), command.Source, ProjectLimits.MaxLabel)
            ?? CommandChecks.Range(nameof(command.BudgetSeconds), command.BudgetSeconds, 0, ProjectLimits.MaxSeconds)
            ?? (command.Created < 0 ? CommandChecks.OutOfRange("Created is a time since 1970, not negative.") : null);
        if (invalid is not null)
        {
            return invalid;
        }
        ScoreMeta score = new()
        {
            Abc = command.Abc,
            Truncated = command.Truncated,
            BudgetSeconds = command.BudgetSeconds,
            Style = command.Style,
            Lyrics = command.Lyrics,
            Cot = command.Cot,
            Seed = command.Seed,
            Model = command.Model,
            EngineId = command.EngineId,
            Label = command.Label,
            Parent = command.Parent,
            Source = command.Source,
            Created = command.Created,
            Extra = clip.Meta?.Score?.Extra ?? ExtraFields.Empty,
        };
        return CommandResult.Ok(MetaEdits.Replace(document, MetaEdits.WithScore(clip, score)));
    }

    private static CommandResult SetLabel(ProjectDocument document, SetScoreLabel command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        if (clip.Meta?.Score is not { } score)
        {
            return CommandChecks.Rejected("The clip has no score to label.");
        }
        return CommandChecks.Length(nameof(command.Label), command.Label, ProjectLimits.MaxName)
            ?? CommandResult.Ok(MetaEdits.Replace(document, MetaEdits.WithScore(clip, score with { Label = command.Label })));
    }

    private static CommandResult ClearScore(ProjectDocument document, ClearClipScore command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        return CommandResult.Ok(MetaEdits.Replace(document, MetaEdits.WithScore(clip, null)));
    }

    private static CommandResult SetStem(ProjectDocument document, SetClipStem command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        if (MetaEdits.Audio(document, command.Source, out AudioClip source) is { } noSource)
        {
            return noSource;
        }
        if (command.Source == command.Clip)
        {
            return CommandChecks.Rejected("A clip cannot be a stem of itself.");
        }
        CommandResult? invalid = CommandChecks.Named(command.Stem)
            ?? CommandChecks.Length(nameof(command.Stem), command.Stem, ProjectLimits.MaxLabel);
        if (invalid is not null)
        {
            return invalid;
        }
        AudioClip stem = MetaEdits.WithStem(clip, new StemOf(command.Source, command.Stem));
        if (LyricsRules.IsVocalStem(command.Stem) && source.Meta?.Lyrics is { } lyrics)
        {
            // The stem is the part of the source that was audible, so its lyrics are the source's, rebased to start at 0.
            stem = MetaEdits.WithLyrics(stem, LyricsRules.Rebase(lyrics, -source.Offset));
        }
        return CommandResult.Ok(MetaEdits.Replace(document, stem));
    }

    private static CommandResult ClearStem(ProjectDocument document, ClearClipStem command, IIdService ids)
    {
        if (MetaEdits.Audio(document, command.Clip, out AudioClip clip) is { } refused)
        {
            return refused;
        }
        return CommandResult.Ok(MetaEdits.Replace(document, MetaEdits.WithStem(clip, null)));
    }

    // A score's parent is another audio clip of the project, and a chain of parents must not lead back to the clip.
    private static CommandResult? Parent(ProjectDocument document, AudioClip clip, ClipId parent)
    {
        if (MetaEdits.Audio(document, parent, out _) is { } missing)
        {
            return missing;
        }
        if (parent == clip.Id)
        {
            return CommandChecks.Rejected("A score cannot be a variation of its own clip.");
        }
        return ClipLinks.ParentLoops(document.Tracks, clip.Id, parent)
            ? CommandChecks.Rejected("That parent is itself a variation of this clip, which would make the versions a loop.")
            : null;
    }
}
