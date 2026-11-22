using AudioLab.Web.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Components;

/// <summary>The catalogue tabs: one is the tab stop, the arrow keys, Home and End move between them, and the active one is strong with an underline.</summary>
public sealed partial class Tabs
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    private string _active = string.Empty;

    private string? _focus;

    /// <summary>The tab names, in order.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<string> Names { get; set; } = [];

    /// <summary>What the tabs are for, as a screen reader says it.</summary>
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    /// <summary>The panel for a tab, given its name; only the active tab's panel is drawn.</summary>
    [Parameter]
    public RenderFragment<string>? TabContent { get; set; }

    [Inject]
    private IElementFocus Focus { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!Names.Contains(_active))
        {
            _active = Names.Count > 0 ? Names[0] : string.Empty;
        }
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (_focus is not null)
        {
            Focus.Focus(TabId(_focus));
            _focus = null;
        }
    }

    private static string AriaSelected(string tab, string active) => tab == active ? "true" : "false";

    private string AriaSelected(string tab) => AriaSelected(tab, _active);

    private string TabId(string tab) => $"tab-{_id}-{Slug(tab)}";

    private string PanelId(string tab) => $"tabpanel-{_id}-{Slug(tab)}";

    private static string Slug(string tab) => tab.ToLowerInvariant().Replace(' ', '-');

    private void Activate(string tab)
    {
        _active = tab;
    }

    private void KeyDown(string tab, KeyboardEventArgs press)
    {
        int at = Names.ToList().IndexOf(tab);
        int? target = press.Key switch
        {
            "ArrowRight" => (at + 1) % Names.Count,
            "ArrowLeft" => (at - 1 + Names.Count) % Names.Count,
            "Home" => 0,
            "End" => Names.Count - 1,
            _ => null,
        };
        if (target is int next)
        {
            _active = Names[next];
            _focus = _active;
        }
    }
}
