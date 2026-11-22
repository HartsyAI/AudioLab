using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Takes a send before or after the fader.</summary>
public sealed record SetSendPre : Command
{
    /// <summary>The track that sends.</summary>
    [Description("The track that sends.")]
    public required TrackId Track { get; init; }

    /// <summary>The bus it sends to.</summary>
    [Description("The bus it sends to.")]
    public required TrackId Bus { get; init; }

    /// <summary>True to send before the fader, false after it.</summary>
    [Description("True to send before the fader, false after it.")]
    public required bool Pre { get; init; }
}
