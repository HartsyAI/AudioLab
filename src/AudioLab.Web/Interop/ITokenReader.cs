namespace AudioLab.Web.Interop;

/// <summary>Reads a design token from the style sheet, so a value drawn from C# is declared once, in <c>tokens.css</c>.</summary>
public interface ITokenReader
{
    /// <summary>The value of a custom property on the root element, such as <c>--track-3</c>, trimmed.</summary>
    public string Read(string token);
}
