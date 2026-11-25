using System.Text;
using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Holds the LRC file tests: which picked files are read as lyrics, and why the others are refused.</summary>
public sealed class LrcFileTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>A file of timed lines is read as its text.</summary>
    [Fact]
    public void ATimedFileIsReadAsItsText()
    {
        (string? text, string? refusal) = LrcFile.Read(Utf8("[00:02.00]alpha beta\n[00:06.50]gamma\n"));
        Assert.Null(refusal);
        Assert.Equal("[00:02.00]alpha beta\n[00:06.50]gamma\n", text);
    }

    /// <summary>A byte order mark is not part of the lyrics.</summary>
    [Fact]
    public void AByteOrderMarkIsNotPartOfTheText()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Utf8("[00:01.00]one\n")];
        (string? text, string? refusal) = LrcFile.Read(bytes);
        Assert.Null(refusal);
        Assert.Equal("[00:01.00]one\n", text);
    }

    /// <summary>Bytes that are not UTF-8 are refused, rather than read with replaced characters.</summary>
    [Fact]
    public void BytesThatAreNotUtf8AreRefused()
    {
        (string? text, string? refusal) = LrcFile.Read([0x5B, 0x30, 0x30, 0x5D, 0xC3, 0x28, 0x0A]);
        Assert.Null(text);
        Assert.Equal("That file is not UTF-8 text, so it was not read.", refusal);
    }

    /// <summary>A file with a NUL byte is refused as not a text file.</summary>
    [Fact]
    public void ABinaryFileIsRefusedAsNotText()
    {
        (string? text, string? refusal) = LrcFile.Read([0x5B, 0x00, 0x5D]);
        Assert.Null(text);
        Assert.Equal("That file is not a text file, so it was not read.", refusal);
    }

    /// <summary>A text file with no timed line is refused: it is not an LRC file.</summary>
    [Fact]
    public void AFileWithoutTimedLinesIsRefused()
    {
        (string? text, string? refusal) = LrcFile.Read(Utf8("just some words\nand more\n"));
        Assert.Null(text);
        Assert.Equal("That file has no timed lines, so it is not an LRC file.", refusal);
    }

    /// <summary>An empty file is refused with its reason.</summary>
    [Fact]
    public void AnEmptyFileIsRefused()
    {
        (string? text, string? refusal) = LrcFile.Read([]);
        Assert.Null(text);
        Assert.Equal("That file is empty.", refusal);
    }

    /// <summary>Text longer than a clip may hold is refused before it is parsed.</summary>
    [Fact]
    public void TextLongerThanAClipMayHoldIsRefused()
    {
        string text = "[00:01.00]" + new string('a', ProjectLimits.MaxLongText);
        (string? read, string? refusal) = LrcFile.Read(Utf8(text));
        Assert.Null(read);
        Assert.Equal("That file holds more text than a clip's lyrics may, so it was not read.", refusal);
    }
}
