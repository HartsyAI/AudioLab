using System.ComponentModel;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Renames the clips that start inside a section after it: the section's name, then the track's.</summary>
public sealed record NameClipsAfterSections : Command
{
    /// <summary>The section whose clips are renamed; every section's clips when absent.</summary>
    [Description("The section whose clips are renamed; every section's clips when absent.")]
    [JsonConverter(typeof(OptionalIdJsonConverter<SectionId>))]
    public SectionId? Section { get; init; }
}
