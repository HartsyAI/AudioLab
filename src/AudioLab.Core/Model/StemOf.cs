using System.Text.Json.Serialization;

namespace AudioLab.Core.Model;

/// <summary>The clip a stem was separated from, and which stem it is.</summary>
/// <param name="ClipId">The source clip, or null when it no longer exists.</param>
/// <param name="Stem">The stem name, such as vocals or drums.</param>
/// <param name="SourceOffset">Where in the source file the stem's audio began when it was cut, in seconds; null when not recorded.</param>
public readonly record struct StemOf(
    [property: JsonConverter(typeof(OptionalIdJsonConverter<ClipId>))] ClipId? ClipId,
    string Stem,
    double? SourceOffset = null);
