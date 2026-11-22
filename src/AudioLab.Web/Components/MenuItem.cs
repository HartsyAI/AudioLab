namespace AudioLab.Web.Components;

/// <summary>One command in a menu: what it says, its shortcut, and whether it destroys something.</summary>
/// <param name="Id">The command's id, which the menu hands back when it is chosen.</param>
/// <param name="Label">What the command says, in sentence case.</param>
/// <param name="Shortcut">The shortcut as the shortcut registry writes it, or null when the command has none.</param>
/// <param name="Destructive">True for a command that deletes something; it sits last and takes the record colour.</param>
/// <param name="Disabled">True when the command cannot run now.</param>
public readonly record struct MenuItem(string Id, string Label, string? Shortcut = null, bool Destructive = false, bool Disabled = false);
