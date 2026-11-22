namespace AudioLab.Core.Commands;

/// <summary>Every command AudioLab.Core ships, in one registry for the server, the UI preview and MCP to share.</summary>
public static class CoreCommands
{
    /// <summary>A new registry holding every command of every domain.</summary>
    public static CommandRegistry CreateRegistry()
    {
        CommandRegistryBuilder builder = new();
        Register(builder);
        return builder.Build();
    }

    /// <summary>Adds every domain's commands to <paramref name="builder"/>.</summary>
    public static void Register(CommandRegistryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        TimelineCommandJsonContext timeline = TimelineCommandJsonContext.Default;
        TempoCommands.Register(builder, timeline);
        MarkerCommands.Register(builder, timeline);
        SectionCommands.Register(builder, timeline);
    }
}
