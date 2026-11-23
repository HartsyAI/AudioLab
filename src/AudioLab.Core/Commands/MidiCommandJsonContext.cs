using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>The source-generated JSON contract of the MIDI commands, as strict as the project document.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowOutOfOrderMetadataProperties = true,
    AllowDuplicateProperties = false,
    NumberHandling = JsonNumberHandling.Strict)]
[JsonSerializable(typeof(NoteId))]
[JsonSerializable(typeof(NoteTarget))]
[JsonSerializable(typeof(NoteLength))]
[JsonSerializable(typeof(ImportedNote))]
[JsonSerializable(typeof(ImportedControlChange))]
[JsonSerializable(typeof(ImportMidiClip))]
[JsonSerializable(typeof(AddNote))]
[JsonSerializable(typeof(MoveNotes))]
[JsonSerializable(typeof(ResizeNotes))]
[JsonSerializable(typeof(SetNoteVelocity))]
[JsonSerializable(typeof(TransposeNotes))]
[JsonSerializable(typeof(QuantizeNotes))]
[JsonSerializable(typeof(DeleteNotes))]
[JsonSerializable(typeof(AddControlChange))]
[JsonSerializable(typeof(RemoveControlChanges))]
[JsonSerializable(typeof(ClearControlChanges))]
public sealed partial class MidiCommandJsonContext : JsonSerializerContext;
