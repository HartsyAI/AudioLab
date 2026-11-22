namespace AudioLab.Web.Components;

/// <summary>The outline of each icon on a 24 by 24 grid, drawn as a stroke in the current text colour.</summary>
/// <remarks>The paths are Lucide's, except the logo (ISC licence; the notice is wwwroot/icons/LICENSE.txt).</remarks>
internal static class IconPaths
{
    private static string Dot(int x) => $"M{x} 10.6a1.4 1.4 0 1 1 0 2.8 1.4 1.4 0 0 1 0-2.8Z";

    /// <summary>Whether an icon is filled, as the transport glyphs and the pointer are; every other icon is a stroke.</summary>
    public static bool IsFilled(IconName name) => name is IconName.ToStart or IconName.Stop or IconName.Play or IconName.Record or IconName.Pointer;

    /// <summary>The path data of an icon.</summary>
    public static string Of(IconName name)
    {
        return name switch
        {
            IconName.Logo => "M3 11v2M7.5 7v10M12 3v18M16.5 8v8M21 11v2",
            IconName.Plus => "M12 5v14M5 12h14",
            IconName.Chevron => "m6 9 6 6 6-6",
            IconName.Close => "M18 6 6 18M6 6l12 12",
            IconName.Pencil => "M17 3a2.85 2.85 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z",
            IconName.Trash => "M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2",
            IconName.More => Dot(5) + Dot(12) + Dot(19),
            IconName.Check => "M20 6 9 17l-5-5",
            IconName.ToStart => "M3.4 4.3h3.1v15.4H3.4ZM20.6 4.3v15.4L8.6 12Z",
            IconName.Stop => "M5.6 5.6h12.8v12.8H5.6Z",
            IconName.Play => "M7.5 4.4 19.8 12 7.5 19.6Z",
            IconName.Record => "M12 5.4a6.6 6.6 0 1 0 0 13.2 6.6 6.6 0 0 0 0-13.2Z",
            IconName.Pointer => "M5.4 3 19 13.6l-5.6.8 3.2 6.2-2.1 1-3.1-6.3-5.6 4.4Z",
            IconName.Range => "M7.7 3.4h8.6M12 3.4v17.2M7.7 20.6h8.6",
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "That icon is not in the set."),
        };
    }
}
