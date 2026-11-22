using System.Globalization;
using AudioLab.Core.Model;
using AudioLab.Web.Components;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The status bar: the connection to the server and what the document holds. The engine's figures wait for an engine.</summary>
public sealed partial class StatusBar
{
    /// <summary>The document.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>The revision of <see cref="Document"/>.</summary>
    [Parameter]
    public long Revision { get; set; }

    /// <summary>The colour of the state dot.</summary>
    [Parameter]
    public StatusDot Dot { get; set; } = StatusDot.Caution;

    /// <summary>The state in words.</summary>
    [Parameter]
    public string State { get; set; } = string.Empty;

    private string Counts
    {
        get
        {
            int clips = Document.Tracks.Sum(track => track.Clips.Count + track.MidiClips.Count);
            return string.Create(CultureInfo.InvariantCulture, $"{Document.Tracks.Count} tracks, {clips} clips");
        }
    }

    private string RevisionText => string.Create(CultureInfo.InvariantCulture, $"Revision {Revision}");
}
