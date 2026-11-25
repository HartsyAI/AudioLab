using AudioLab.Core.Model;
using AudioLab.Web.Session;

namespace AudioLab.Web.Tests;

/// <summary>Which lane a pass records: the one lane in a recording mode the screen can choose, never two, never a mode it cannot choose (D86).</summary>
public sealed class RecordingLaneTests
{
    private static readonly Func<AutomationMode, string?> _allAvailable = _ => null;

    private static ProjectDocument Document(params (string Id, AutomationMode Mode)[] lanes) =>
        Song.Empty().With(lanes.Select(lane => Lane(lane.Id, lane.Mode)).ToArray());

    private static Track Lane(string id, AutomationMode mode)
    {
        Track track = Song.NewTrack(id);
        return track with { Automation = track.Automation with { Mode = mode } };
    }

    /// <summary>A lane in a recording mode records nothing while the screen cannot choose that mode, so no pass can start from it.</summary>
    [Fact]
    public void ALaneInARecordingModeIsNotRecordedWhileTheModeCannotBeChosen()
    {
        ProjectDocument document = Document(("t1", AutomationMode.Write));

        Assert.Null(RecordingLane.Find(document));
    }

    /// <summary>A lane in a recording mode the screen can choose is the one lane a pass records, with its parameter.</summary>
    [Fact]
    public void ANamedRecordingLaneIsTheOneThatRecords()
    {
        ProjectDocument document = Document(("t1", AutomationMode.Touch), ("t2", AutomationMode.Off));

        (TrackId track, AutomationParameter parameter, AutomationMode mode)? found = RecordingLane.Find(document, _allAvailable);

        Assert.Equal(TrackId.From("t1"), found?.track);
        Assert.Equal(AutomationParameter.Volume, found?.parameter);
        Assert.Equal(AutomationMode.Touch, found?.mode);
    }

    /// <summary>Two lanes in recording modes record neither: a pass writes one lane, and which one would be a guess.</summary>
    [Fact]
    public void TwoRecordingLanesRecordNeither()
    {
        ProjectDocument document = Document(("t1", AutomationMode.Touch), ("t2", AutomationMode.Latch));

        Assert.Null(RecordingLane.Find(document, _allAvailable));
    }

    /// <summary>Off and read lanes record nothing, even when every mode can be chosen.</summary>
    [Fact]
    public void APlayingLaneRecordsNothing()
    {
        ProjectDocument document = Document(("t1", AutomationMode.Read), ("t2", AutomationMode.Off));

        Assert.Null(RecordingLane.Find(document, _allAvailable));
    }
}
