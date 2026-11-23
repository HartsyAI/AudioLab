using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace AudioLab.Audio.Tests.Support;

/// <summary>Compares a render with a stored one, or writes it when the environment variable AUDIOLAB_UPDATE_GOLDEN is 1.</summary>
/// <remarks>A fixture is the left channel then the right, as little-endian 32-bit floats, in the Golden folder beside the tests.</remarks>
public static class GoldenFile
{
    /// <summary>How far a sample may differ from the stored one; the platforms' maths libraries differ by far less.</summary>
    public const float Tolerance = 2e-4f;

    private const string UpdateVariable = "AUDIOLAB_UPDATE_GOLDEN";

    public static void Check(string name, RenderedAudio audio, [CallerFilePath] string caller = "")
    {
        string path = Path.Combine(Path.GetDirectoryName(caller)!, "Golden", name + ".f32");
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Assert.True(
                Environment.GetEnvironmentVariable("CI") != "true",
                $"{UpdateVariable} is set in CI, which would make every golden test pass by rewriting its own fixture.");
            Write(path, audio);
            return;
        }
        Assert.True(File.Exists(path), $"No golden file {path}. Create it with {UpdateVariable}=1 and review the result.");
        float[] expected = Read(path);
        Assert.Equal(audio.Left.Length * 2, expected.Length);
        float worst = 0;
        int where = 0;
        for (int i = 0; i < audio.Left.Length; i++)
        {
            float difference = Math.Max(Math.Abs(audio.Left[i] - expected[i]), Math.Abs(audio.Right[i] - expected[audio.Left.Length + i]));
            if (difference > worst)
            {
                worst = difference;
                where = i;
            }
        }
        Assert.True(worst <= Tolerance, $"{name}: frame {where} differs by {worst}. Regenerate with {UpdateVariable}=1 if intended.");
    }

    private static void Write(string path, RenderedAudio audio)
    {
        byte[] bytes = new byte[audio.Frames * 2 * sizeof(float)];
        for (int i = 0; i < audio.Frames; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * sizeof(float)), audio.Left[i]);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan((audio.Frames + i) * sizeof(float)), audio.Right[i]);
        }
        File.WriteAllBytes(path, bytes);
    }

    private static float[] Read(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        float[] all = new float[bytes.Length / sizeof(float)];
        for (int i = 0; i < all.Length; i++)
        {
            all[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(i * sizeof(float)));
        }
        return all;
    }
}
