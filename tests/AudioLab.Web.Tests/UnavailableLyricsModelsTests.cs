using AudioLab.Core.Model;
using AudioLab.Web.Services;

namespace AudioLab.Web.Tests;

/// <summary>Holds the lyrics models seam tests: until the server serves the models, nothing is faked and each request says why.</summary>
public sealed class UnavailableLyricsModelsTests
{
    private readonly UnavailableLyricsModels _models = new();

    /// <summary>The seam names its reason, and both requests are refused with that same reason.</summary>
    [Fact]
    public async Task BothRequestsAreRefusedWithTheReasonTheSeamNames()
    {
        ClipId clip = ClipId.From("a");
        Assert.Equal("Needs the speech model, which is not built yet.", _models.Unavailable);

        EditResult align = await _models.AlignAsync(clip, "some words");
        EditResult transcribe = await _models.TranscribeAsync(clip);

        Assert.False(align.Succeeded);
        Assert.False(transcribe.Succeeded);
        Assert.Equal(_models.Unavailable, align.Refusal);
        Assert.Equal(_models.Unavailable, transcribe.Refusal);
    }
}
