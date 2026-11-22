using System.Text.Json;
using AudioLab.Core.Commands;
using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

// The schema is what an MCP client reads (D11), so every parameter says what it is and what it may be.
public sealed class CommandSchemaContractTests
{
    private readonly CommandRegistry _registry = CoreCommands.CreateRegistry();

    [Fact]
    public void EveryPropertyOfEveryCommandHasADescription()
    {
        List<string> missing = [];

        foreach (CommandDescriptor command in _registry.Commands)
        {
            foreach (JsonProperty property in command.Schema.GetProperty("properties").EnumerateObject())
            {
                bool described = property.Value.TryGetProperty("description", out JsonElement text) && !string.IsNullOrWhiteSpace(text.GetString());
                if (!described)
                {
                    missing.Add($"{command.Name}.{property.Name}");
                }
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void TheTempoIsLimitedToTheProjectsRange()
    {
        foreach ((string command, string member) in new[] { ("tempo_set_bpm", "bpm"), ("tempo_change_add", "bpm"), ("tempo_change_set_bpm", "bpm") })
        {
            JsonElement bpm = Property(command, member);
            Assert.Equal(ProjectLimits.MinBpm, bpm.GetProperty("minimum").GetDouble());
            Assert.Equal(ProjectLimits.MaxBpm, bpm.GetProperty("maximum").GetDouble());
        }
    }

    [Fact]
    public void TheTimeSignatureListsItsAllowedValues()
    {
        JsonElement numerator = Property("time_signature_set", "numerator");
        JsonElement denominator = Property("time_signature_set", "denominator");

        Assert.Equal(1, numerator.GetProperty("minimum").GetDouble());
        Assert.Equal(64, numerator.GetProperty("maximum").GetDouble());
        Assert.Equal([1.0, 2.0, 4.0, 8.0, 16.0, 32.0], denominator.GetProperty("enum").EnumerateArray().Select(item => item.GetDouble()));
    }

    [Fact]
    public void TimesHaveTheirMinimumAndTheMostTheProjectHolds()
    {
        JsonElement change = Property("tempo_change_add", "time");
        JsonElement marker = Property("marker_add", "time");
        JsonElement start = Property("section_add", "start");
        JsonElement length = Property("section_add", "length");

        Assert.Equal(0.01, change.GetProperty("minimum").GetDouble());
        Assert.Equal(ProjectLimits.MaxSeconds, change.GetProperty("maximum").GetDouble());
        Assert.Equal(0, marker.GetProperty("minimum").GetDouble());
        Assert.Equal(0, start.GetProperty("minimum").GetDouble());
        Assert.Equal(Section.MinLength, length.GetProperty("minimum").GetDouble());
    }

    [Fact]
    public void NamesAndColoursHaveTheirLimits()
    {
        JsonElement added = Property("marker_add", "name");
        JsonElement renamed = Property("section_rename", "name");
        JsonElement color = Property("marker_set_color", "color");

        Assert.Equal(ProjectLimits.MaxName, added.GetProperty("maxLength").GetInt32());
        Assert.Equal(1, renamed.GetProperty("minLength").GetInt32());
        Assert.Equal(ProjectLimits.MaxName, renamed.GetProperty("maxLength").GetInt32());
        Assert.Equal(ProjectColor.SchemaPattern, color.GetProperty("pattern").GetString());
    }

    [Fact]
    public void ASectionEditListsItsThreeKinds()
    {
        JsonElement edit = Property("section_set_span", "edit");

        Assert.Equal(["move", "start", "end"], edit.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void OnlyTheValuesAnEditNeedsAreRequired()
    {
        JsonElement schema = _registry.Find("section_set_span")!.Schema;
        string[] required = [.. schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()!)];

        Assert.Equal(["edit", "project", "section"], required.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheColourPatternMatchesWhatTheValidatorAccepts()
    {
        System.Text.RegularExpressions.Regex pattern = new(ProjectColor.SchemaPattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        foreach (string good in new[] { "#abc", "#aabbcc", "#aabbccdd", "#ABC123" })
        {
            Assert.True(pattern.IsMatch(good) && ProjectColor.IsValid(good), good);
        }
        foreach (string bad in new[] { "abc", "#ab", "#abcd", "#aabbccd", "#ggg", "red", "#abc\n" })
        {
            Assert.False(ProjectColor.IsValid(bad), bad);
        }
        Assert.DoesNotMatch(pattern, "#abcd");
    }

    [Fact]
    public void FadersAndPansHaveTheirRanges()
    {
        JsonElement volume = Property("mixer_set_track_volume", "volume");
        JsonElement pan = Property("mixer_set_track_pan", "pan");
        JsonElement send = Property("mixer_send_set_level", "level");

        Assert.Equal((0.0, ProjectLimits.MaxGain), (volume.GetProperty("minimum").GetDouble(), volume.GetProperty("maximum").GetDouble()));
        Assert.Equal((-1.0, 1.0), (pan.GetProperty("minimum").GetDouble(), pan.GetProperty("maximum").GetDouble()));
        Assert.Equal(ProjectLimits.MaxGain, send.GetProperty("maximum").GetDouble());
    }

    [Fact]
    public void AnInstrumentPresetListsTheNamesWeShipAndADirectionListsUpAndDown()
    {
        JsonElement preset = Property("track_add", "preset");
        JsonElement direction = Property("track_move", "direction");

        Assert.Equal(InstrumentPresets.Names, preset.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["up", "down"], direction.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void AListOfTracksNeedsOneToTheMostATrackLimitAllows()
    {
        foreach (string command in new[] { "group_create", "group_assign" })
        {
            JsonElement tracks = Property(command, "tracks");
            Assert.Equal(1, tracks.GetProperty("minItems").GetInt32());
            Assert.Equal(ProjectLimits.MaxTracks, tracks.GetProperty("maxItems").GetInt32());
            Assert.False(tracks.TryGetProperty("minLength", out _));
        }
    }

    [Fact]
    public void AClipCommandStatesItsLimitsAndKinds()
    {
        JsonElement duration = Property("clip_add", "duration");
        JsonElement start = Property("clip_add", "start");
        JsonElement length = Property("midi_clip_add", "length");
        JsonElement edge = Property("clip_trim", "edge");
        JsonElement layer = Property("clip_reorder", "layer");
        JsonElement gain = Property("clip_set_gain", "gain");

        Assert.Equal(ProjectLimits.MaxSeconds, duration.GetProperty("maximum").GetDouble());
        Assert.True(duration.GetProperty("minimum").GetDouble() > 0);
        Assert.Equal(0, start.GetProperty("minimum").GetDouble());
        Assert.Equal(MidiClip.MinLength, length.GetProperty("minimum").GetDouble());
        Assert.Equal(["start", "end"], edge.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["front", "back"], layer.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(ProjectLimits.MaxGain, gain.GetProperty("maximum").GetDouble());
        Assert.Equal(["clip", "edge", "project", "time"], Required("clip_trim"));
        foreach ((string command, string member) in new[] { ("clip_move", "start"), ("clip_trim", "time"), ("clip_split", "at"), ("clip_set_fades", "fadeIn"), ("clip_set_fades", "fadeOut") })
        {
            JsonElement property = Property(command, member);
            Assert.Equal((0.0, ProjectLimits.MaxSeconds), (property.GetProperty("minimum").GetDouble(), property.GetProperty("maximum").GetDouble()));
        }
        Assert.Equal(MidiClip.MinLength, Property("midi_clip_set_length", "length").GetProperty("minimum").GetDouble());
        Assert.Equal(1, Property("clip_rename", "name").GetProperty("minLength").GetInt32());
        Assert.Equal(["clip", "project"], Required("clip_delete"));
    }

    [Fact]
    public void AMidiCommandStatesItsRangesAndTheNoteList()
    {
        JsonElement pitch = Property("midi_note_add", "pitch");
        JsonElement velocity = Property("midi_note_add", "velocity");
        JsonElement semitones = Property("midi_notes_transpose", "semitones");
        JsonElement notes = Property("midi_notes_delete", "notes");
        JsonElement optional = Property("midi_notes_transpose", "notes");

        Assert.Equal((0.0, 127.0), (pitch.GetProperty("minimum").GetDouble(), pitch.GetProperty("maximum").GetDouble()));
        Assert.Equal((1.0, 127.0), (velocity.GetProperty("minimum").GetDouble(), velocity.GetProperty("maximum").GetDouble()));
        Assert.Equal((-127.0, 127.0), (semitones.GetProperty("minimum").GetDouble(), semitones.GetProperty("maximum").GetDouble()));
        Assert.Equal("array", notes.GetProperty("type").GetString());
        Assert.Equal(1, notes.GetProperty("minItems").GetInt32());
        Assert.Equal("integer", notes.GetProperty("items").GetProperty("type").GetString());
        Assert.Equal(["clip", "notes", "project"], Required("midi_notes_move"));
        Assert.DoesNotContain("notes", Required("midi_notes_transpose"));
        Assert.Equal(1, optional.GetProperty("minItems").GetInt32());
        Assert.Equal(ProjectLimits.MaxListItems, optional.GetProperty("maxItems").GetInt32());
        JsonElement target = Property("midi_notes_move", "notes").GetProperty("items");
        Assert.Equal(["note", "pitch", "start"], target.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal));
        Assert.True(target.GetProperty("properties").GetProperty("start").TryGetProperty("description", out _));
        Assert.True(optional.TryGetProperty("description", out _));
    }

    [Fact]
    public void TheEffectSettingAndPresetNamesInTheSchemaAreExactlyTheRulesOwn()
    {
        string[] settings = [.. Property("effect_set_parameter", "parameter").GetProperty("enum").EnumerateArray().Select(item => item.GetString()!)];
        string[] presets = [.. Property("effect_add", "preset").GetProperty("enum").EnumerateArray().Select(item => item.GetString()!)];
        string[] applied = [.. Property("effect_apply_preset", "preset").GetProperty("enum").EnumerateArray().Select(item => item.GetString()!)];

        Assert.Equal(EffectRules.AllParameterNames.Order(StringComparer.Ordinal), settings.Order(StringComparer.Ordinal));
        Assert.Equal(EffectPresets.AllNames.Order(StringComparer.Ordinal), presets.Order(StringComparer.Ordinal));
        Assert.Equal(presets, applied);
    }

    [Fact]
    public void TheValueOfAnEffectSettingStatesEverySettingsRange()
    {
        string description = Property("effect_set_parameter", "value").GetProperty("description").GetString()!;
        System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;

        foreach (EffectKind kind in Enum.GetValues<EffectKind>())
        {
            foreach (EffectParameter setting in EffectRules.Parameters(kind))
            {
                string range = $"{setting.Name} {setting.Min.ToString("0.###", invariant)} to {setting.Max.ToString("0.###", invariant)}";
                Assert.Contains(range, description, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void AnEffectCommandListsItsKindsPresetsAndDivisions()
    {
        JsonElement kind = Property("effect_add", "kind");
        JsonElement division = Property("effect_set_delay_sync", "division");

        Assert.Equal(["eq", "compressor", "reverb", "delay", "saturation"], kind.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(EffectRules.Divisions, division.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["effect", "parameter", "project", "track", "value"], Required("effect_set_parameter"));
    }

    [Fact]
    public void AnAutomationValueStatesBothLanesRangesAndTheSchemaBoundIsTheirUnion()
    {
        foreach ((string command, string member) in new[] { ("automation_point_add", "value"), ("automation_point_move", "value") })
        {
            JsonElement value = Property(command, member);
            Assert.Equal((-1.0, AutomationRules.VolumeMax), (value.GetProperty("minimum").GetDouble(), value.GetProperty("maximum").GetDouble()));
            string description = value.GetProperty("description").GetString()!;
            Assert.Contains($"volume {AutomationRules.Min(AutomationParameter.Volume)} to {AutomationRules.Max(AutomationParameter.Volume)}", description);
            Assert.Contains($"pan {AutomationRules.Min(AutomationParameter.Pan)} to {AutomationRules.Max(AutomationParameter.Pan)}", description);
        }
        Assert.Equal(["volume", "pan"], Property("automation_point_add", "parameter").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            ["read", "touch", "latch", "write", "off"],
            Property("automation_set_mode", "mode").GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["parameter", "project", "time", "track", "value"], Required("automation_point_add"));
        Assert.DoesNotContain("visible", Required("automation_set_view"));
    }

    [Fact]
    public void ThePointListsOfAutomationCommandsAreBoundedAndDescribeTheirItems()
    {
        foreach (string command in new[] { "automation_points_set_values", "automation_write_commit" })
        {
            JsonElement points = Property(command, "points");
            Assert.Equal(1, points.GetProperty("minItems").GetInt32());
            Assert.Equal(ProjectLimits.MaxListItems, points.GetProperty("maxItems").GetInt32());
            JsonElement properties = points.GetProperty("items").GetProperty("properties");
            Assert.All(properties.EnumerateObject(), property => Assert.True(property.Value.TryGetProperty("description", out _), property.Name));
            Assert.All(properties.EnumerateObject(), property => Assert.True(property.Value.TryGetProperty("maximum", out _), property.Name));
        }
        JsonElement items = Property("automation_points_set_values", "points").GetProperty("items");
        Assert.Equal(["index", "value"], items.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal));
        Assert.Equal(ProjectLimits.MaxListItems - 1, Property("automation_point_move", "index").GetProperty("maximum").GetInt32());
    }

    [Fact]
    public void TheBeatCommandsStateTheValidatorsRangesAndTheStepList()
    {
        JsonElement steps = Property("beat_pattern_set_steps", "steps");
        JsonElement swing = Property("beat_pattern_set_swing", "swing");
        JsonElement gain = Property("beat_lane_set_gain", "gain");
        JsonElement list = Property("beat_lane_set_steps", "steps");

        Assert.Equal((1.0, (double)ProjectLimits.MaxBeatSteps), (steps.GetProperty("minimum").GetDouble(), steps.GetProperty("maximum").GetDouble()));
        Assert.Equal((0.0, 1.0), (swing.GetProperty("minimum").GetDouble(), swing.GetProperty("maximum").GetDouble()));
        Assert.Equal((0.0, ProjectLimits.MaxGain), (gain.GetProperty("minimum").GetDouble(), gain.GetProperty("maximum").GetDouble()));
        Assert.Equal((1, ProjectLimits.MaxBeatSteps), (list.GetProperty("minItems").GetInt32(), list.GetProperty("maxItems").GetInt32()));
        Assert.Equal(["lane", "on", "project", "steps"], Required("beat_lane_set_steps"));
        Assert.DoesNotContain("media", Required("beat_lane_add"));
        Assert.DoesNotContain("name", Required("beat_lane_add"));
        Assert.Equal(ProjectLimits.MaxName, Property("beat_lane_add", "name").GetProperty("maxLength").GetInt32());
    }

    private string[] Required(string command)
    {
        return [.. _registry.Find(command)!.Schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal)];
    }

    private JsonElement Property(string command, string member)
    {
        return _registry.Find(command)!.Schema.GetProperty("properties").GetProperty(member);
    }
}
