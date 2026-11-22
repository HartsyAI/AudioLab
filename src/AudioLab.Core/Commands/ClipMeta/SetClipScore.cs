using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets the score record of an audio clip: every field given replaces the old one, and a field left out is removed.</summary>
public sealed record SetClipScore : Command
{
    /// <summary>The audio clip to edit.</summary>
    [Description("The audio clip to edit.")]
    public required ClipId Clip { get; init; }

    /// <summary>The score in ABC notation, up to 1000000 characters.</summary>
    [Description("The score in ABC notation, up to 1000000 characters.")]
    [MaxLength(ProjectLimits.MaxLongText)]
    public string? Abc { get; init; }

    /// <summary>Whether the engine cut the score short to fit its budget.</summary>
    [Description("Whether the engine cut the score short to fit its budget.")]
    public bool? Truncated { get; init; }

    /// <summary>The length the engine was allowed, in seconds.</summary>
    [Description("The length the engine was allowed, in seconds.")]
    [Range(0, ProjectLimits.MaxSeconds)]
    public double? BudgetSeconds { get; init; }

    /// <summary>The style prompt, up to 1000000 characters.</summary>
    [Description("The style prompt, up to 1000000 characters.")]
    [MaxLength(ProjectLimits.MaxLongText)]
    public string? Style { get; init; }

    /// <summary>The lyrics prompt, up to 1000000 characters.</summary>
    [Description("The lyrics prompt, up to 1000000 characters.")]
    [MaxLength(ProjectLimits.MaxLongText)]
    public string? Lyrics { get; init; }

    /// <summary>How much planning the engine did, up to 128 characters.</summary>
    [Description("How much planning the engine did, up to 128 characters.")]
    [MaxLength(ProjectLimits.MaxLabel)]
    public string? Cot { get; init; }

    /// <summary>The seed of the render.</summary>
    [Description("The seed of the render.")]
    [Range(long.MinValue, long.MaxValue)]
    public long? Seed { get; init; }

    /// <summary>The model that rendered it, up to 128 characters.</summary>
    [Description("The model that rendered it, up to 128 characters.")]
    [MaxLength(ProjectLimits.MaxLabel)]
    public string? Model { get; init; }

    /// <summary>The engine that rendered it, up to 128 characters.</summary>
    [Description("The engine that rendered it, up to 128 characters.")]
    [MaxLength(ProjectLimits.MaxLabel)]
    public string? EngineId { get; init; }

    /// <summary>The version label shown in the score tab, up to 200 characters.</summary>
    [Description("The version label shown in the score tab, up to 200 characters.")]
    [MaxLength(ProjectLimits.MaxName)]
    public string? Label { get; init; }

    /// <summary>The audio clip this score is a variation of; a root version when absent.</summary>
    [Description("The audio clip this score is a variation of; a root version when absent.")]
    [JsonConverter(typeof(OptionalIdJsonConverter<ClipId>))]
    public ClipId? Parent { get; init; }

    /// <summary>How the score came to be, such as planned or rendered, up to 128 characters.</summary>
    [Description("How the score came to be, such as planned or rendered, up to 128 characters.")]
    [MaxLength(ProjectLimits.MaxLabel)]
    public string? Source { get; init; }

    /// <summary>When it was made, in milliseconds since 1970.</summary>
    [Description("When it was made, in milliseconds since 1970.")]
    [Range(0, long.MaxValue)]
    public long? Created { get; init; }
}
