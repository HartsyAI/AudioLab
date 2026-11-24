using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Web.Services;

/// <summary>The edits of an effect chain the session screen makes, each one command sent by wire name; refusals come back as words.</summary>
/// <param name="live">The open project.</param>
/// <param name="sendTogether">Sends commands built for the open project as one gesture, so one undo step; the MIDI editor's sender is one.</param>
public sealed class EffectEditor(
    LiveProject live,
    Func<Func<ProjectId, IReadOnlyList<Command>>, Task<EditResult>> sendTogether)
{
    /// <summary>Adds an effect of a kind with its starting settings at the end of a chain; its id is in <see cref="EditResult.Created"/>.</summary>
    public Task<EditResult> AddAsync(TrackId track, EffectKind kind)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new AddEffect { Project = project, Track = track, Kind = kind }));
    }

    /// <summary>Removes an effect from a track's chain.</summary>
    public Task<EditResult> RemoveAsync(TrackId track, EffectId effect)
    {
        return EditRunner.RunAsync(
            live,
            project => live.SendAsync(new RemoveEffect { Project = project, Track = track, Effect = effect }));
    }

    /// <summary>Puts an effect in the signal path or bypasses it.</summary>
    public Task<EditResult> SetEnabledAsync(TrackId track, EffectId effect, bool enabled)
    {
        return EditRunner.RunAsync(
            live,
            project => live.SendAsync(new SetEffectEnabled { Project = project, Track = track, Effect = effect, Enabled = enabled }));
    }

    /// <summary>Moves an effect by places, negative for earlier: one <c>effect_move</c> each, sent as one gesture and so one undo step.</summary>
    public Task<EditResult> MoveAsync(TrackId track, EffectId effect, int places)
    {
        return sendTogether(project =>
        {
            MoveDirection direction = places < 0 ? MoveDirection.Up : MoveDirection.Down;
            return [.. Enumerable.Range(0, Math.Abs(places)).Select(_ => Step(project, track, effect, direction))];
        });
    }

    /// <summary>Sets one numeric setting to an absolute value: one gesture, one command, one undo step.</summary>
    public Task<EditResult> SetParameterAsync(TrackId track, EffectId effect, string parameter, double value)
    {
        return EditRunner.RunAsync(live, async project =>
        {
            string gesture = live.NewGesture();
            try
            {
                return await live.SendAsync(
                    new SetEffectParameter { Project = project, Track = track, Effect = effect, Parameter = parameter, Value = value },
                    gesture);
            }
            finally
            {
                await live.EndGestureAsync(gesture);
            }
        });
    }

    /// <summary>Sets whether a delay follows the tempo, and its note division when one is given.</summary>
    public Task<EditResult> SetDelaySyncAsync(TrackId track, EffectId effect, bool sync, string? division)
    {
        return EditRunner.RunAsync(
            live,
            project => live.SendAsync(new SetDelaySync { Project = project, Track = track, Effect = effect, Sync = sync, Division = division }));
    }

    /// <summary>Replaces an effect's settings with a preset's; its place and bypass stay.</summary>
    public Task<EditResult> ApplyPresetAsync(TrackId track, EffectId effect, string preset)
    {
        return EditRunner.RunAsync(
            live,
            project => live.SendAsync(new ApplyEffectPreset { Project = project, Track = track, Effect = effect, Preset = preset }));
    }

    private static MoveEffect Step(ProjectId project, TrackId track, EffectId effect, MoveDirection direction)
    {
        return new MoveEffect { Project = project, Track = track, Effect = effect, Direction = direction };
    }
}
