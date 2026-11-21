using Bit.Bheadless;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>A field that filters a long list as the user types and picks one entry from it.</summary>
public sealed partial class AlCombobox
{
    private string _query = string.Empty;

    /// <summary>The chosen option, or null for none.</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Raised when the user chooses an option.</summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>The entries to search.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<string> Options { get; set; } = [];

    /// <summary>The accessible name of the field.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>The hint shown in the empty field.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    private IEnumerable<string> Matches => Options.Where(option => BitBhFilter.Contains(option, _query));

    private void SetQuery(string query)
    {
        _query = query;
    }
}
