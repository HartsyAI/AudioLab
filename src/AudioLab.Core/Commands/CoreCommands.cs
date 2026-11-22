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
        TrackCommandJsonContext tracks = TrackCommandJsonContext.Default;
        GroupCommands.Register(builder, tracks);
        TrackCommands.Register(builder, tracks);
        TrackSettingsCommands.Register(builder, tracks);
        MixerCommands.Register(builder, tracks);
        ClipCommands.Register(builder, ClipCommandJsonContext.Default);
        MidiCommands.Register(builder, MidiCommandJsonContext.Default);
        EffectCommands.Register(builder, EffectCommandJsonContext.Default);
        AutomationCommands.Register(builder, AutomationCommandJsonContext.Default);
        BeatCommands.Register(builder, BeatCommandJsonContext.Default);
        LyricsCommands.Register(builder, LyricsCommandJsonContext.Default);
        ClipMetaCommands.Register(builder, ClipMetaCommandJsonContext.Default);
        ProjectCommands.Register(builder, ProjectCommandJsonContext.Default);
        InstrumentCommands.Register(builder, InstrumentCommandJsonContext.Default);
    }
}
