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
    public void ANoteIdFollowsTheHighestInItsClip()
    {
        IdService service = new();
        Assert.Equal(NoteId.First, service.NextNote([]));
        Assert.Equal(new NoteId(8), service.NextNote([new MidiNote(new NoteId(2), 60, 0, 1, 100), new MidiNote(new NoteId(7), 62, 1, 1, 100)]));
    }
}
