using AudioLab.Core.Model;

namespace AudioLab.Web.Session;

/// <summary>Which lane a pass records (D86): the one lane in a recording mode, and only when the screen can choose that mode (D71).</summary>
public static class RecordingLane
{
    /// <summary>The lane a pass records, or null.</summary>
    /// <remarks>Null when no lane is in a recording mode, when more than one is (neither records, since a pass writes one lane), or when the mode cannot be chosen yet.</remarks>
    /// <param name="document">The project as it is now.</param>
    /// <param name="unavailable">The reason a mode cannot be chosen on a lane, or null; the screen's rule unless a test says otherwise.</param>
    public static (TrackId Track, AutomationParameter Parameter, AutomationMode Mode)? Find(
        ProjectDocument document,
        Func<AutomationMode, AutomationParameter, string?>? unavailable = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        List<Track> lanes = document.Tracks
            .Where(track => track.Automation.Mode is AutomationMode.Write or AutomationMode.Touch or AutomationMode.Latch)
            .ToList();
        if (lanes.Count != 1)
        {
            return null;
        }
        Track lane = lanes[0];
        AutomationMode mode = lane.Automation.Mode;
        return (unavailable ?? AutomationModes.UnavailableReason)(mode, lane.View.AutomationParameter) is null
            ? (lane.Id, lane.View.AutomationParameter, mode)
            : null;
    }
}
