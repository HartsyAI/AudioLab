using System.Text.Json.Serialization;

namespace AudioLab.Conventions.Tests;

/// <summary>Source-generated JSON metadata for reading project.assets.json. A repeated key is an error, not last-one-wins.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, AllowDuplicateProperties = false)]
[JsonSerializable(typeof(AssetsDocument))]
internal sealed partial class AssetsJsonContext : JsonSerializerContext;
