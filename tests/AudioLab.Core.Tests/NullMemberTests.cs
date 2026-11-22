using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class NullMemberTests
{
    public static TheoryData<string, string, Func<ProjectDocument, ProjectDocument>> Documents => new()
    {
        { "a null project name", "name: is null", project => project with { Name = null! } },
        { "a null track list", "tracks: is null", project => project with { Tracks = null! } },
        { "a null track", "tracks[0]: is null", project => project with { Tracks = [null!] } },
        { "a null beat pattern", "beatPattern: is null", project => project with { BeatPattern = null! } },
        { "a null view", "view: is null", project => project with { View = null! } },
        { "a null track name", "tracks[0].name: is null", project => WithTrack(project, track => track with { Name = null! }) },
        { "null automation", "tracks[0].automation: is null", project => WithTrack(project, track => track with { Automation = null! }) },
        { "a null clip list", "tracks[0].clips: is null", project => WithTrack(project, track => track with { Clips = null! }) },
        { "a null clip", "tracks[0].clips[1]: is null", project => WithTrack(project, track => track with { Clips = [track.Clips[0], null!] }) },
        { "a null effect", "tracks[0].effects[0]: is null", project => WithTrack(project, track => track with { Effects = [null!] }) },
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void ANullWhereAValueIsRequiredIsReportedWithItsPathAndNeverThrowsFromTheValidator(
        string description,
        string expected,
        Func<ProjectDocument, ProjectDocument> breakIt)
    {
        ProjectDocument broken = breakIt(SampleProject.Create(UserId.Local));
        IReadOnlyList<string> problems = ProjectValidator.Validate(broken);
        Assert.True(problems.Any(problem => problem.Contains(expected, StringComparison.Ordinal)), $"{description}: {string.Join("; ", problems)}");
        ProjectFormatException error = Assert.Throws<ProjectFormatException>(() => ProjectJson.Write(broken));
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnIdThatWasNeverSetIsAnInvalidIdAndNotANull()
    {
        ProjectDocument project = SampleProject.Create(UserId.Local);
        Assert.Equal(string.Empty, default(TrackId).Value);
        Assert.Contains(ProjectValidator.Validate(project with { Id = default }), problem => problem.StartsWith("id:", StringComparison.Ordinal));
        Track track = project.Tracks[0] with { Id = default };
        Assert.NotEmpty(ProjectValidator.Validate(project with { Tracks = [track, .. project.Tracks.Skip(1)] }));
    }

    [Theory]
    [InlineData("swarm", "")]
    [InlineData("swarm", "a-b")]
    public void ForDoorNamesTheArgumentThatIsWrong(string door, string subject)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => UserId.ForDoor(door, subject));
        Assert.Equal("subject", error.ParamName);
        Assert.Equal("door", Assert.Throws<ArgumentException>(() => UserId.ForDoor("sw-arm", "a")).ParamName);
    }

    [Fact]
    public void ForDoorNamesTheSubjectWhenTheTwoTogetherAreTooLong()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => UserId.ForDoor("swarm", new string('a', 64)));
        Assert.Equal("subject", error.ParamName);
    }

    private static ProjectDocument WithTrack(ProjectDocument project, Func<Track, Track> change)
    {
        return project with { Tracks = [change(project.Tracks[0]), .. project.Tracks.Skip(1)] };
    }
}
