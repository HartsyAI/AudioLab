namespace AudioLab.Web.Session;

/// <summary>A named section of the song, shown in the ruler.</summary>
public readonly record struct SectionModel(string Name, int StartBar, int Bars, string ColourToken);
