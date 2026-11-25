using System.Text;
using AudioLab.Web.Components.Lyrics;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components.Forms;

namespace AudioLab.Web.Tests;

/// <summary>Holds the picked-file tests: the size check comes before any read, a failed read says so, and a pick always hands the chooser on.</summary>
public sealed class LrcPickTests
{
    /// <summary>A file the browser cannot read is refused with the words for an unreadable file, not thrown.</summary>
    [Fact]
    public async Task AFileTheBrowserCannotReadIsRefusedWithItsWords()
    {
        FakeFile file = new(size: 10, open: () => throw new IOException("the browser refused the read"));
        (string? text, string? refusal) = await LrcFile.ReadPickedAsync(file);
        Assert.Null(text);
        Assert.Equal("That file could not be read.", refusal);
    }

    /// <summary>A file larger than a clip may hold is refused by its size, before the browser is asked to read it.</summary>
    [Fact]
    public async Task AFileLargerThanAClipMayHoldIsRefusedBeforeItIsRead()
    {
        bool opened = false;
        FakeFile file = new(size: LrcFile.MaxBytes + 1, open: () =>
        {
            opened = true;
            return new MemoryStream();
        });
        (string? text, string? refusal) = await LrcFile.ReadPickedAsync(file);
        Assert.Null(text);
        Assert.Equal(LrcFile.TooLarge, refusal);
        Assert.False(opened);
    }

    /// <summary>A timed file is read through the browser and its text is returned.</summary>
    [Fact]
    public async Task ATimedFileIsReadThroughTheBrowser()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("[00:01.00]one\n");
        FakeFile file = new(size: bytes.Length, open: () => new MemoryStream(bytes));
        (string? text, string? refusal) = await LrcFile.ReadPickedAsync(file);
        Assert.Null(refusal);
        Assert.Equal("[00:01.00]one\n", text);
    }

    /// <summary>A pick hands the chooser on after its handling succeeds.</summary>
    [Fact]
    public async Task APickMovesTheChooserOnWhenItSucceeds()
    {
        ChooserTurn turn = new();
        await turn.PickAsync(() => Task.CompletedTask);
        Assert.Equal(1, turn.Number);
    }

    /// <summary>A pick whose handling fails with something other than a read error still moves the chooser on, and the failure goes on.</summary>
    [Fact]
    public async Task APickMovesTheChooserOnEvenWhenItsHandlingFails()
    {
        ChooserTurn turn = new();
        await Assert.ThrowsAsync<InvalidOperationException>(() => turn.PickAsync(() => throw new InvalidOperationException("the edit failed")));
        Assert.Equal(1, turn.Number);
    }

    private sealed class FakeFile(long size, Func<Stream> open) : IBrowserFile
    {
        public string Name => "test.lrc";

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public long Size { get; } = size;

        public string ContentType => "text/plain";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => open();
    }
}
