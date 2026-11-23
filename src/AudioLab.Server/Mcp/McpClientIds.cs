using AudioLab.Core.Commands;

namespace AudioLab.Server.Mcp;

/// <summary>Issues the client id an agent's command is applied under: one per call, never a hub connection's and never the agent's choice.</summary>
public class McpClientIds
{
    /// <summary>What every client id of an agent starts with, so it can never be a hub connection id.</summary>
    public const string Prefix = "mcp-";

    /// <summary>A new id, valid as a <see cref="ClientId"/>, that no other call has.</summary>
    public virtual ClientId Issue() => ClientId.From(Prefix + Guid.NewGuid().ToString("N"));
}
