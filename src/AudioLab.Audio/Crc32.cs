namespace AudioLab.Audio;

/// <summary>The CRC-32 that a ZIP file records for each of its entries.</summary>
internal static class Crc32
{
    private static readonly uint[] _table = BuildTable();

    /// <summary>The check value after <paramref name="bytes"/> have been added to <paramref name="state"/>, which starts at zero.</summary>
    public static uint Update(uint state, ReadOnlySpan<byte> bytes)
    {
        uint value = ~state;
        foreach (byte b in bytes)
        {
            value = _table[(value ^ b) & 0xFF] ^ (value >> 8);
        }
        return ~value;
    }

    private static uint[] BuildTable()
    {
        uint[] table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return table;
    }
}
