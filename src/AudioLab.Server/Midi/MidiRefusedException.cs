using AudioLab.Core.Commands;

namespace AudioLab.Server.Midi;

/// <summary>A MIDI import or export that was refused; the message is safe to show the client and names only the user's own things.</summary>
public sealed class MidiRefusedException : Exception
{
    /// <summary>Refuses with the class of the reason, which decides the status of the answer.</summary>
    public MidiRefusedException(CommandErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>The class of the reason, as a command's refusal has it.</summary>
    public CommandErrorCode Code { get; }
}
