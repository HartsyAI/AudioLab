namespace AudioLab.Web.Components;

/// <summary>One command in a menu: what it says, its shortcut, and whether it destroys something.</summary>
public readonly record struct AlMenuItem(string Id, string Label, string? Shortcut = null, bool Destructive = false, bool Disabled = false);
