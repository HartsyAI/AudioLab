using System.Globalization;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Services;

/// <summary>The edits of MIDI clips the session screen makes, each one command sent by wire name; the server's reasons come back as words.</summary>
public sealed class MidiEditor(LiveProject live)
{
    private bool _adding;

    /// <summary>Adds an empty clip of one bar to an instrument track at <paramref name="start"/> or after its last clip.</summary>
    /// <remarks>A second call while the first is on its way does nothing: the place of the next clip depends on the first being drawn.</remarks>
    public async Task<EditResult> AddClipAsync(TrackId track, double? start = null)
    {
        if (_adding)
        {
            return EditResult.Applied(changed: false, []);
        }
        _adding = true;
        try
        {
            return await EditRunner.RunAsync(live, project =>
            {
                ProjectDocument document = live.Document!;
                Track? target = document.Tracks.FirstOrDefault(candidate => candidate.Id == track);
                double at = Math.Max(0, start ?? (target is null ? 0 : MidiClipPlan.NextStart(document, target)));
                double length = MidiClipPlan.DefaultLength(document, at);
                return live.SendAsync(new AddMidiClip { Project = project, Track = track, Start = at, Length = length });
            });
        }
        finally
        {
            _adding = false;
        }
    }

    /// <summary>Sends commands as one gesture, so they are one undo step; all the ids they allocate are in the result, in order.</summary>
    /// <remarks>
    /// A command that is refused, or a connection that is lost, after others were applied does not take them back: undo is shared by every
    /// client, so a take-back could undo another client's step. The result says how many were applied, and an undo takes them back as one step.
    /// </remarks>
    public async Task<EditResult> SendAsync(Func<ProjectId, IReadOnlyList<Command>> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        if (live.Document is not { } document)
        {
            return EditResult.Refused("The project is not open yet.");
        }
        IReadOnlyList<Command> commands = build(document.Id);
        if (commands.Count <= 1)
        {
            return commands.Count == 0 ? EditResult.Applied(changed: false, []) : await EditRunner.RunAsync(live, _ => live.SendAsync(commands[0]));
        }
        string gesture = live.NewGesture();
        List<string> created = [];
        bool changed = false;
        try
        {
            for (int index = 0; index < commands.Count; index++)
            {
                Command command = commands[index];
                EditResult result = await EditRunner.RunAsync(live, _ => live.SendAsync(command, gesture));
                if (!result.Succeeded)
                {
                    return EditResult.Refused(Partial(result.Refusal, index, commands.Count));
                }
                changed |= result.Changed;
                created.AddRange(result.Created);
            }
        }
        finally
        {
            await EndAsync(gesture);
        }
        return EditResult.Applied(changed, created);
    }

    private const string Undoable = "Undo takes the applied ones back as one step if no one else has edited meanwhile.";

    /// <summary>What a refusal in the middle of a gesture says: the reason, and how much of the edit was applied.</summary>
    public static string Partial(string? reason, int applied, int total)
    {
        string why = string.IsNullOrWhiteSpace(reason) ? "The server refused the edit." : reason;
        return applied == 0
            ? why
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{why} {applied} of {total} commands of this edit were applied and the rest were not; {Undoable}");
    }

    private async Task EndAsync(string gesture)
    {
        try
        {
            await live.EndGestureAsync(gesture);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HubException or OperationCanceledException)
        {
            // The connection went: the server closes the gesture of a client that is gone.
        }
    }

    /// <summary>Sets the grid the project snaps to.</summary>
    public Task<EditResult> SetGridAsync(string grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetProjectView { Project = project, Grid = grid }));
    }

    /// <summary>Sets a clip's length to an absolute number of seconds.</summary>
    public Task<EditResult> SetLengthAsync(ClipId clip, double length)
    {
        return EditRunner.RunAsync(live, project => live.SendAsync(new SetMidiClipLength { Project = project, Clip = clip, Length = length }));
    }

    /// <summary>Moves a clip's end by whole bars, through the tempo map; shortening it past the least length is refused with the reason.</summary>
    public async Task<EditResult> StepLengthAsync(ClipId clip, int bars)
    {
        if (live.Document is not { } document || MidiClipPlan.Find(document, clip) is not { } found)
        {
            return EditResult.Refused("That MIDI clip is not in the project.");
        }
        return MidiClipPlan.Stepped(document, found.Clip, bars) is { } length
            ? await SetLengthAsync(clip, length)
            : EditResult.Refused(string.Create(CultureInfo.InvariantCulture, $"A MIDI clip is at least {MidiClip.MinLength} seconds long."));
    }
}
