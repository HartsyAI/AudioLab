using AudioLab.Core.Commands;

namespace AudioLab.Server.Commands;

/// <summary>Builds the server's one command registry: every command AudioLab.Core ships.</summary>
public static class ServerCommands
{
    /// <summary>The registry of every command the server accepts.</summary>
    public static CommandRegistry Create() => CoreCommands.CreateRegistry();
}
