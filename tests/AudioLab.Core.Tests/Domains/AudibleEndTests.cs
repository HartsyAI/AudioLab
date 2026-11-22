using AudioLab.Core.Model;
using AudioLab.Core.Rules;

namespace AudioLab.Core.Tests;

public sealed class AudibleEndTests
{
    private static AudioClip Clip(string id, double start, double length, bool muted = false) => new()
    {
        Id = ClipId.From(id),
        Name = id,
        Media = MediaId.From("m"),
        Start = start,
        Duration = length,
        Offset = 0,
        TrimEnd = 0,
        Gain = 1,
        FadeIn = 0,
        FadeOut = 0,
        Muted = muted,
    };

    private static Track WithClips(string id, params AudioClip[] clips) => BlankProject.Track(id) with { Clips = new ValueList<AudioClip>(clips) };

    private static ProjectDocument Project(params Track[] tracks) => BlankProject.Create() with { Tracks = new ValueList<Track>(tracks) };

    [Fact]
    public void EveryClipCountsByDefaultAndOnlyAudibleOnesWhenAsked()
    {
        ProjectDocument document = Project(WithClips("a", Clip("c1", 0, 4), Clip("c2", 10, 5, muted: true)));

        Assert.Equal(15, ProjectContent.End(document));
        Assert.Equal(4, ProjectContent.End(document, audibleOnly: true));
    }

    [Fact]
    public void AMutedTrackOrOneSilencedBySoloDoesNotExtendTheAudibleEnd()
    {
        Track muted = WithClips("m", Clip("c1", 20, 5)) with { Muted = true };
        Track quiet = WithClips("q", Clip("c2", 30, 5));
        Track soloed = WithClips("s", Clip("c3", 0, 3)) with { Soloed = true };

        Assert.Equal(25, ProjectContent.End(Project(muted, soloed)));
        Assert.Equal(3, ProjectContent.End(Project(muted, quiet, soloed), audibleOnly: true));
    }

    [Fact]
    public void ATrackSilencedBySoloStillCountsWhenASendOfItIsHeard()
    {
        Track bus = BlankProject.Track("bus", kind: TrackKind.Bus) with { Soloed = true };
        Track feeder = WithClips("f", Clip("c1", 0, 9)) with { Sends = [new Send(TrackId.From("bus"), 1, false, false)] };

        Assert.Equal(9, ProjectContent.End(Project(feeder, bus), audibleOnly: true));
    }
}
