using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The clip a stem was separated from, and which stem it is.</summary>
/// <param name="ClipId">The source clip, or null when it no longer exists.</param>
/// <param name="Stem">The stem name, such as vocals or drums.</param>
public readonly record struct StemOf(
    [property: JsonConverter(typeof(OptionalIdJsonConverter<ClipId>))] ClipId? ClipId,
    string Stem);
