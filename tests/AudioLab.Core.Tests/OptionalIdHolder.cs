using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

/// <summary>A type with one optional id, to test the converter on its own.</summary>
public sealed class OptionalIdHolder
{
    [JsonConverter(typeof(OptionalIdJsonConverter<ClipId>))]
    public ClipId? Link { get; set; }
}
