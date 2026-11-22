using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Sets how the arrangement opens; give at least one of the ruler, the edit mode and the grid.</summary>
public sealed record SetProjectView : Command
{
    /// <summary>Which ruler shows first, time or beats; it keeps its value when absent.</summary>
    [Description("Which ruler shows first, time or beats; it keeps its value when absent.")]
    public RulerMode? Ruler { get; init; }

    /// <summary>How edits snap and move other material: slip, grid or shuffle; it keeps its value when absent.</summary>
    [Description("How edits snap and move other material: slip, grid or shuffle; it keeps its value when absent.")]
    public EditMode? EditMode { get; init; }

    /// <summary>The grid, one of the values listed; it keeps its value when absent.</summary>
    /// <remarks>
    /// The grid: auto (which follows the zoom of the view), bar, a note value such as 1/8 or 1/16t, or a time
    /// such as 0.5s; it keeps its value when absent.
    /// </remarks>
    [Description(
        "The grid: auto (which follows the zoom of the view), bar, a note value such as 1/8 or 1/16t, or a " +
        "time such as 0.5s; it keeps its value when absent.")]
    [AllowedValues(
        "auto", "bar", "1/2", "1/4", "1/8", "1/16", "1/32", "1/8t", "1/16t", "1s", "0.5s", "0.1s", "10ms")]
    public string? Grid { get; init; }
}
