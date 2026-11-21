using System.Text.Json.Serialization;

namespace AudioLab.Web.Bench;

/// <summary>Source-generated JSON for the benchmark report.</summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(BenchReport))]
public sealed partial class BenchJsonContext : JsonSerializerContext
{
}
