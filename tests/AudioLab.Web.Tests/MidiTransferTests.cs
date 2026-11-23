using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>The seam for .mid files refuses with its reason and never reports a success it did not have.</summary>
public sealed class MidiTransferTests
{
    /// <summary>Every call is refused with the one reason, and nothing is created.</summary>
    [Fact]
    public async Task EveryCallIsRefusedWithTheReasonAndNothingIsCreated()
    {
        UnavailableMidiTransfer transfer = new();

        EditResult import = await transfer.ImportAsync(TrackId.From("keys"), 4);
        EditResult export = await transfer.ExportAsync(ClipId.From("clip"));

        Assert.NotNull(transfer.Unavailable);
        Assert.False(import.Succeeded);
        Assert.False(export.Succeeded);
        Assert.Equal(transfer.Unavailable, import.Refusal);
        Assert.Equal(transfer.Unavailable, export.Refusal);
        Assert.Empty(import.Created);
    }
}
