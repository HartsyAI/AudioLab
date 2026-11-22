using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class TrackIconTests
{
    [Theory]
    [InlineData("🎤")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("data:image/webp;base64,UklGRg==")]
    public void ShortTextAndSmallRasterImagesAreAllowed(string icon)
    {
        Assert.True(TrackIcon.IsValid(icon));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/icon.png")]
    [InlineData("data:image/svg+xml;base64,PHN2Zz48L3N2Zz4=")]
    [InlineData("data:image/png;base64,<script>")]
    [InlineData("data:image/png;base64,AAAA\n")]
    public void RemoteAddressesVectorImagesAndMarkupAreRefused(string icon)
    {
        Assert.False(TrackIcon.IsValid(icon));
    }

    [Fact]
    public void AnOversizeImageIsRefused()
    {
        Assert.False(TrackIcon.IsValid("data:image/png;base64," + new string('A', TrackIcon.MaxImageLength)));
    }

    [Fact]
    public void ADocumentWithAnUnsafeIconIsNotWritten()
    {
        ProjectDocument project = SampleProject.Create(UserId.Local);
        Track unsafeTrack = project.Tracks[0] with { Icon = "https://example.com/icon.png" };
        ProjectDocument broken = project with { Tracks = [unsafeTrack, .. project.Tracks.Skip(1)] };
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(broken));
        Assert.Contains("tracks[0].icon", error.Message);
    }
}
