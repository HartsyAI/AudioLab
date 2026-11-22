using System.Text.Json.Serialization;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

// Commands that exist only to exercise the registry and the history; the real domains ship their own.
internal sealed record RenameProjectCommand : Command
{
    public required string Name { get; init; }
}

internal sealed record SetBpmCommand : Command
{
    public required double Bpm { get; init; }
}

internal sealed record AddMarkerCommand : Command
{
    public required double Time { get; init; }

    public required string Name { get; init; }
}

internal sealed record NothingCommand : Command;

internal sealed record RefusedCommand : Command;

internal sealed record HijackCommand : Command
{
    public required UserId NewOwner { get; init; }
}

internal sealed record BreakCommand : Command
{
    public required int Count { get; init; }
}

internal sealed record ChangeIdCommand : Command;

internal sealed record ChangeVersionCommand : Command;

internal sealed record MintOnlyCommand : Command;

internal sealed record UnregisteredCommand : Command;

internal record UnsealedCommand : Command;

internal sealed record DerivedCommand : UnsealedCommand;

internal sealed record ShapeCommand : Command
{
    [JsonConverter(typeof(OptionalIdJsonConverter<TrackId>))]
    public TrackId? Track { get; init; }

    [JsonConverter(typeof(ValueListJsonConverter<TrackId>))]
    public required ValueList<TrackId> Tracks { get; init; }

    public required TrackKind Kind { get; init; }

    public TrackKind? Mode { get; init; }

    public required double Level { get; init; }

    [JsonConverter(typeof(ValueListJsonConverter<bool>))]
    public required ValueList<bool> Flags { get; init; }

    [JsonConverter(typeof(ValueListJsonConverter<string>))]
    public required ValueList<string> Labels { get; init; }
}
