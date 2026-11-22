using System.Buffers.Binary;

namespace AudioLab.SpikeServer;

/// <summary>Makes the spike's test clips: a few seconds of plucked notes, stereo, 16-bit PCM, as a WAV file.</summary>
public static class ClipSynth
{
    /// <summary>Frames per second of the clips; 44.1 kHz, so the engine has to resample on a 48 kHz output.</summary>
    public const int SampleRate = 44100;

    /// <summary>Builds clip number <paramref name="index" />, <paramref name="seconds" /> long.</summary>
    public static byte[] Create(int index, int seconds)
    {
        int frames = SampleRate * seconds;
        byte[] file = new byte[44 + (frames * 4)];
        WriteHeader(file, frames * 4);
        double root = 110 * Math.Pow(2, (index * 3 % 12) / 12.0);
        Random random = new(index + 1);
        Span<byte> body = file.AsSpan(44);
        double noteLength = 0.5 + (0.125 * (index % 4));
        for (int frame = 0; frame < frames; frame++)
        {
            double time = frame / (double)SampleRate;
            int note = (int)(time / noteLength);
            double age = time - (note * noteLength);
            double frequency = root * Math.Pow(2, new[] { 0, 3, 7, 10, 12, 7, 5, 3 }[(note + index) % 8] / 12.0);
            double envelope = Math.Exp(-age * 5);
            double tone = (0.5 * Math.Sin(2 * Math.PI * frequency * time)) + (0.25 * Math.Sin(4 * Math.PI * frequency * time)) + (0.1 * Math.Sin(6 * Math.PI * frequency * time));
            double noise = (random.NextDouble() - 0.5) * 0.02;
            short left = (short)(short.MaxValue * 0.6 * ((tone * envelope) + noise));
            short right = (short)(short.MaxValue * 0.6 * ((tone * envelope * 0.8) - noise));
            BinaryPrimitives.WriteInt16LittleEndian(body[(frame * 4)..], left);
            BinaryPrimitives.WriteInt16LittleEndian(body[((frame * 4) + 2)..], right);
        }
        return file;
    }

    private static void WriteHeader(byte[] file, int dataBytes)
    {
        "RIFF"u8.CopyTo(file);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(4), 36 + dataBytes);
        "WAVE"u8.CopyTo(file.AsSpan(8));
        "fmt "u8.CopyTo(file.AsSpan(12));
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(22), 2);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(24), SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(28), SampleRate * 4);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(32), 4);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(34), 16);
        "data"u8.CopyTo(file.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(40), dataBytes);
    }
}
