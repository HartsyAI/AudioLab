using AudioLab.Core.Model;

namespace AudioLab.Core.Tests;

public sealed class IdServiceTests
{
    [Fact]
    public void AllocatedIdsAreUniqueAndCarryTheirKindPrefix()
    {
        IdService service = new();
        HashSet<string> seen = [];
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(service.Next<TrackId>().Value));
            Assert.True(seen.Add(service.Next<ClipId>().Value));
        }
        Assert.StartsWith("trk_", service.Next<TrackId>().Value);
        Assert.StartsWith("clp_", service.Next<ClipId>().Value);
        Assert.StartsWith("med_", service.Next<MediaId>().Value);
    }

    [Fact]
    public void IdsAllocatedFromManyThreadsAtOnceAreStillUnique()
    {
        IdService service = new();
        string[][] batches = new string[8][];
        Parallel.For(0, batches.Length, batch =>
        {
            batches[batch] = [.. Enumerable.Range(0, 5000).Select(_ => service.Next<ClipId>().Value)];
        });
        string[] all = [.. batches.SelectMany(batch => batch)];
        Assert.Equal(all.Length, all.Distinct().Count());
    }

    [Fact]
    public void EveryAllocatedIdIsWellFormed()
    {
        IdService service = new();
        Assert.True(IdRules.IsValid(service.Next<ProjectId>().Value));
        Assert.True(IdRules.IsValid(service.Next<EffectId>().Value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../../evil")]
    [InlineData("a/b")]
    [InlineData("a\b")]
    [InlineData("a b")]
    [InlineData("..")]
    [InlineData("name.with.dots")]
    [InlineData("id\nline")]
    [InlineData("naïve")]
    public void AnIdThatCouldReachAPathOrAStyleIsRefused(string text)
    {
        Assert.False(IdRules.IsValid(text));
        Assert.Throws<ArgumentException>(() => TrackId.From(text));
    }

    [Fact]
    public void AnIdLongerThanTheCapIsRefused()
    {
        Assert.True(IdRules.IsValid(new string('a', IdRules.MaxLength)));
        Assert.False(IdRules.IsValid(new string('a', IdRules.MaxLength + 1)));
        Assert.False(IdRules.IsValid(null));
    }

    [Fact]
    public void TheLocalUserBelongsToTheDesktopDoorAndNoOtherDoorCanMakeIt()
    {
        Assert.Equal("desktop-local", UserId.Local.Value);
        Assert.NotEqual(UserId.Local, UserId.ForDoor("swarm", "local"));
        Assert.Equal("swarm-alice", UserId.ForDoor("swarm", "alice").Value);
        Assert.Throws<ArgumentException>(() => UserId.ForDoor("swarm", "../x"));
        Assert.Throws<ArgumentException>(() => UserId.ForDoor(string.Empty, "x"));
    }

    [Fact]
    public void NoteIdsCountUpFromOne()
    {
        Assert.Equal(1, NoteId.First.Value);
        Assert.Equal(new NoteId(2), NoteId.First.Next());
        Assert.Throws<OverflowException>(() => new NoteId(int.MaxValue).Next());
    }
}
