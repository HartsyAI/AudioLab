using System.Text.Json.Serialization;

namespace AudioLab.Conventions.Tests;

/// <summary>Source-generated JSON metadata for reading project.assets.json.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AssetsDocument))]
internal sealed partial class AssetsJsonContext : JsonSerializerContext;
