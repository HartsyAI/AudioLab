using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Mutes or unmutes a send.</summary>
public sealed record SetSendMuted : Command
{
    /// <summary>The track that sends.</summary>
    [Description("The track that sends.")]
    public required TrackId Track { get; init; }

    /// <summary>The bus it sends to.</summary>
    [Description("The bus it sends to.")]
    public required TrackId Bus { get; init; }

    /// <summary>True to silence the send.</summary>
    [Description("True to silence the send.")]
    public required bool Muted { get; init; }
}
