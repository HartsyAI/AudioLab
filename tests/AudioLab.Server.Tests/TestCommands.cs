using AudioLab.Core.Commands;

namespace AudioLab.Server.Tests;

internal sealed record RenameCommand : Command
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

internal sealed record RefuseCommand : Command;

internal sealed record FailWithCommand : Command
{
    public required string Code { get; init; }
}

internal sealed record BadBpmCommand : Command;

internal sealed record NoChangeCommand : Command;

internal sealed record AddManyCommand : Command
{
    public required int Count { get; init; }
}

internal sealed record SlowCommand : Command;

internal sealed record BulkCommand : Command
{
    public required string Text { get; init; }
}
