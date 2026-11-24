using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue dropdown menu: a button that opens commands, each with its shortcut, destructive ones last.</summary>
/// <remarks>The list renders in place, never in a portal, so the wrapper's root holds it and its <c>::deep</c> styles match.</remarks>
public sealed partial class Menu
{
    /// <summary>The button's text, and its accessible name when the button shows only an icon.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>True for a square button with three dots in place of the label.</summary>
    [Parameter]
    public bool Icon { get; set; }

    /// <summary>The id of the menu's wrapper, so that a shortcut can move the focus to its button; the library chooses the button's own id.</summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>The commands.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<MenuItem> Items { get; set; } = [];

    /// <summary>Raised with the chosen command.</summary>
    [Parameter]
    public EventCallback<MenuItem> OnSelect { get; set; }

    // Destructive commands come last; the sort is stable, so the caller's order holds inside each group.
    private IEnumerable<MenuItem> Ordered => Order(Items);

    // The chevron is the same nine pixels as the one beside the grid in the top bar; Icon takes pixels and the tokens have no icon scale.
    private const int ChevronSize = 9;

    private string? ButtonName => Icon ? Label : null;

    // While the menu is open its keys are the menu's: typing a letter finds an item, and no shortcut of the screen behind it hears the letter.
    private bool _open;

    private Task OpenChangedAsync(bool open)
    {
        _open = open;
        return Task.CompletedTask;
    }

    /// <summary>The commands in the order the menu shows them: the others as given, then the destructive ones as given.</summary>
    /// <param name="items">The commands as the caller listed them.</param>
    /// <returns>The same commands, destructive ones last.</returns>
    internal static IEnumerable<MenuItem> Order(IEnumerable<MenuItem> items) => items.OrderBy(item => item.Destructive);

    private Task SelectAsync(MenuItem item)
    {
        return OnSelect.InvokeAsync(item);
    }
}
