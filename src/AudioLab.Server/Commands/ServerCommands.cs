using AudioLab.Core.Commands;

namespace AudioLab.Server.Commands;

/// <summary>Builds the server's one command registry. Each domain's commands are registered here as they are written.</summary>
public static class ServerCommands
{
    /// <summary>The registry of every command the server accepts.</summary>
    public static CommandRegistry Create() => new CommandRegistryBuilder().Build();
}
