using SkiaSharp;

namespace AudioLab.Web.Rendering;

/// <summary>Loads the Inter weights Skia draws with from the files the app serves.</summary>
public static class SkiaFontLoader
{
    private static readonly int[] _weights = [400, 500, 600];

    /// <summary>Loads each weight into a typeface; a weight Skia cannot decode is left out and reported in <paramref name="failures"/>.</summary>
    public static async Task<Dictionary<int, SKTypeface>> LoadAsync(HttpClient http, string extension, List<string> failures)
    {
        Dictionary<int, SKTypeface> faces = [];
        foreach (int weight in _weights)
        {
            string file = $"fonts/inter-latin-{weight}-normal.{extension}";
            byte[] bytes = await http.GetByteArrayAsync(file);
            SKTypeface? face = SKTypeface.FromData(SKData.CreateCopy(bytes));
            if (face is null)
            {
                failures.Add($"{file}: Skia could not decode it");
            }
            else
            {
                faces[weight] = face;
            }
        }
        return faces;
    }
}
