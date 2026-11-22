using System.Text.Json.Serialization;

namespace AudioLab.Core.Tests;

// Default options: unknown members are ignored, so the registry must refuse it.
[JsonSerializable(typeof(NothingCommand))]
internal sealed partial class LaxCommandJsonContext : JsonSerializerContext;
