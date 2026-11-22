using AudioLab.Core.Model;
using AudioLab.Web.Session;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Components;

/// <summary>The catalogue track header: colour strip, number, name with M S R I, source with the volume, and the meter.</summary>
public sealed partial class TrackHeader
{
    private Track? _drawn;

    private bool _selected;

    private bool _editable;

    private double _height;

    private string? _color;

    private int _index = -1;

    /// <summary>The track.</summary>
    [Parameter, EditorRequired]
    public Track Track { get; set; } = null!;

    /// <summary>The track's place in the list, from 0.</summary>
    [Parameter]
    public int Index { get; set; }

    /// <summary>Whether the track is the selected one.</summary>
    [Parameter]
    public bool Selected { get; set; }

    /// <summary>The height of the row, from the document.</summary>
    [Parameter]
    public double Height { get; set; }

    /// <summary>The track colour as a style value, already mapped to the nearest track token.</summary>
    [Parameter, EditorRequired]
    public string ColorToken { get; set; } = string.Empty;

    /// <summary>Whether the toggles act; when not, they are disabled.</summary>
    [Parameter]
    public bool Editable { get; set; }

    /// <summary>Raised with the track when its mute toggle is pressed.</summary>
    [Parameter]
    public EventCallback<TrackId> OnMute { get; set; }

    /// <summary>Raised with the track when its solo toggle is pressed.</summary>
    [Parameter]
    public EventCallback<TrackId> OnSolo { get; set; }

    /// <summary>Raised with the track when its name is double-clicked.</summary>
    [Parameter]
    public EventCallback<TrackId> OnRename { get; set; }

    /// <summary>Raised with the track when its header is pressed.</summary>
    [Parameter]
    public EventCallback<TrackId> OnSelect { get; set; }

    private string Number => TrackText.Number(Index);

    private string Source => TrackText.Source(Track);

    private double VolumePosition => FaderScale.Position(Track.Volume);

    private string VolumeText => FaderScale.Text(Track.Volume);

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        bool changed = !ReferenceEquals(_drawn, Track) && !Equals(_drawn, Track);
        changed |= Editable != _editable || Selected != _selected || Height != _height || ColorToken != _color || Index != _index;
        _drawn = Track;
        _selected = Selected;
        _editable = Editable;
        _height = Height;
        _color = ColorToken;
        _index = Index;
        return changed;
    }

    private Task MuteAsync() => OnMute.InvokeAsync(Track.Id);

    private Task SoloAsync() => OnSolo.InvokeAsync(Track.Id);

    private Task RenameAsync() => OnRename.InvokeAsync(Track.Id);

    private Task SelectAsync()
    {
        return OnSelect.InvokeAsync(Track.Id);
    }
}
