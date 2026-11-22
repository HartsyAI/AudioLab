using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class CoreCommandsTests
{
    private readonly CommandRegistry _registry = CoreCommands.CreateRegistry();

    [Fact]
    public void EveryCommandRecordOfTheAssemblyIsRegisteredOnce()
    {
        HashSet<Type> declared =
        [
            .. typeof(Command).Assembly.GetTypes().Where(type => type is { IsAbstract: false, IsPublic: true } && type.IsSubclassOf(typeof(Command))),
        ];

        Assert.Equal(declared.Count, _registry.Commands.Count);
        Assert.Equal(declared, _registry.Commands.Select(command => command.CommandType).ToHashSet());
    }

    [Fact]
    public void EveryCommandNamesItsProjectAndHasAUsableSchema()
    {
        foreach (CommandDescriptor command in _registry.Commands)
        {
            Assert.True(CommandRegistryBuilder.IsValidName(command.Name), command.Name);
            Assert.Equal("object", command.Schema.GetProperty("type").GetString());
            Assert.False(command.Schema.GetProperty("additionalProperties").GetBoolean(), command.Name);
            string[] required = [.. command.Schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()!)];
            Assert.Contains("project", required);
        }
    }

    [Fact]
    public void NoMemberOfAnyCommandSchemaIsEmpty()
    {
        List<string> empty = [];

        foreach (CommandDescriptor command in _registry.Commands)
        {
            SchemaWalk.Empty(command.Schema, command.Name, empty);
        }

        Assert.Empty(empty);
    }

    [Fact]
    public void TheRenamedSectionCommandsExistUnderTheirNewNamesOnly()
    {
        Assert.NotNull(_registry.Find("section_clear"));
        Assert.NotNull(_registry.Find("section_from_markers"));
        Assert.Null(_registry.Find("sections_clear"));
        Assert.Null(_registry.Find("sections_from_markers"));
        Assert.Equal(CommandErrorCode.UnknownCommand, _registry.Parse("sections_clear", "{}").Errors[0].Code);
    }

    [Fact]
    public void TheGroupTrackAndMixerCommandsExist()
    {
        string[] names =
        [
            "group_create", "group_assign", "group_ungroup", "group_rename", "group_set_color", "group_set_collapsed", "group_set_tint",
            "group_set_muted", "group_set_soloed", "group_set_volume", "group_move", "track_add", "track_remove", "track_rename",
            "track_set_color", "track_move", "mixer_set_track_volume", "mixer_set_track_pan", "mixer_set_track_muted",
            "mixer_set_track_soloed", "mixer_set_master_volume", "mixer_set_master_limiter", "mixer_send_add", "mixer_send_remove",
            "mixer_send_set_level", "mixer_send_set_pre", "mixer_send_set_muted",
        ];

        Assert.All(names, name => Assert.NotNull(_registry.Find(name)));
    }

    [Fact]
    public void TheMidiAndEffectCommandsExist()
    {
        string[] names =
        [
            "midi_note_add", "midi_notes_move", "midi_notes_resize", "midi_notes_set_velocity", "midi_notes_transpose",
            "midi_notes_quantize", "midi_notes_delete", "midi_control_add", "midi_control_remove", "midi_control_clear",
            "effect_add", "effect_remove", "effect_move", "effect_set_enabled", "effect_set_parameter", "effect_set_delay_sync",
            "effect_apply_preset",
        ];

        Assert.All(names, name => Assert.NotNull(_registry.Find(name)));
    }

    [Fact]
    public void ANoteListAndAnEffectCommandReadFromTheirJson()
    {
        string project = BlankProject.Create().Id.Value;

        CommandParseResult notes = _registry.Parse("midi_notes_delete", $$"""{"project":"{{project}}","clip":"clp_1","notes":[1,2,7]}""");
        CommandParseResult all = _registry.Parse("midi_notes_move", $$"""{"project":"{{project}}","clip":"clp_1","time":0.5,"pitch":-2}""");
        CommandParseResult effect = _registry.Parse("effect_add", $$"""{"project":"{{project}}","track":"trk_1","kind":"delay","preset":"Slap"}""");
        CommandParseResult zeroNote = _registry.Parse("midi_notes_delete", $$"""{"project":"{{project}}","clip":"clp_1","notes":[0]}""");
        CommandParseResult wrongKind = _registry.Parse("effect_add", $$"""{"project":"{{project}}","track":"trk_1","kind":"chorus"}""");

        Assert.Equal([new NoteId(1), new NoteId(2), new NoteId(7)], Assert.IsType<DeleteNotes>(notes.Command).Notes);
        Assert.Null(Assert.IsType<MoveNotes>(all.Command).Notes);
        Assert.Equal(EffectKind.Delay, Assert.IsType<AddEffect>(effect.Command).Kind);
        Assert.False(zeroNote.Succeeded);
        Assert.False(wrongKind.Succeeded);
    }

    [Fact]
    public void TheClipCommandsExist()
    {
        string[] names =
        [
            "clip_add", "midi_clip_add", "clip_move", "clip_trim", "clip_split", "clip_duplicate", "clip_delete", "clip_set_gain",
            "clip_set_fades", "midi_clip_set_length", "clip_set_muted", "clip_rename", "clip_set_color", "clip_reorder",
        ];

        Assert.All(names, name => Assert.NotNull(_registry.Find(name)));
    }

    [Fact]
    public void TheDomainsShipTheirCommandsUnderStableNames()
    {
        Assert.Contains(_registry.Commands, command => command.Name == "tempo_change_add");
        Assert.Contains(_registry.Commands, command => command.Name == "marker_add");
        Assert.Contains(_registry.Commands, command => command.Name == "section_set_span");
    }

    [Fact]
    public void ACommandRoundTripsThroughItsJsonAndRunsAgainstAProject()
    {
        ProjectDocument project = BlankProject.Create();
        string json = $$"""{"project":"{{project.Id.Value}}","section":"sec_none","start":1,"end":2,"edit":"move"}""";

        CommandParseResult parsed = _registry.Parse("section_set_span", json);

        SetSectionSpan command = Assert.IsType<SetSectionSpan>(parsed.Command);
        Assert.Equal(SectionEdit.Move, command.Edit);
        CommandResult result = _registry.Execute(project, command, new SequentialIdService());
        Assert.Equal(CommandErrorCode.NotFound, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("""{"project":"p","time":1,"name":"x","id":"mrk_chosen"}""")]
    [InlineData("""{"project":"p","edit":"sideways"}""")]
    [InlineData("""{"project":"P"}""")]
    public void AClientCannotChooseIdsOrSendLooseValues(string json)
    {
        Assert.False(_registry.Parse("marker_add", json).Succeeded);
        Assert.False(_registry.Parse("section_set_span", json).Succeeded);
    }

    [Fact]
    public void ARegistryBuiltTwiceIsEquivalent()
    {
        Assert.Equal(_registry.Commands.Select(command => command.Name), CoreCommands.CreateRegistry().Commands.Select(command => command.Name));
        Assert.Equal(
            JsonSerializer.Serialize(_registry.Commands.Select(command => command.Schema)),
            JsonSerializer.Serialize(CoreCommands.CreateRegistry().Commands.Select(command => command.Schema)));
    }
}
