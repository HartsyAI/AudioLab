using System.ComponentModel;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Removes a track's send to a bus.</summary>
public sealed record RemoveSend : Command
{
    /// <summary>The track that sends.</summary>
    [Description("The track that sends.")]
    public required TrackId Track { get; init; }

    /// <summary>The bus it sends to.</summary>
    [Description("The bus it sends to.")]
    public required TrackId Bus { get; init; }
}
