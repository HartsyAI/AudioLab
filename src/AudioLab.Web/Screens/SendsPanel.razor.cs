using AudioLab.Core.Model;
using AudioLab.Web.Components;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Screens;

/// <summary>The sends of the selected track: a menu that adds one to a bus, then a row for each send.</summary>
public sealed partial class SendsPanel
{
    private string? _shown;

    /// <summary>The document, for the names of the buses.</summary>
    [Parameter, EditorRequired]
    public ProjectDocument Document { get; set; } = null!;

    /// <summary>What the screen can do to the project, and why not when it cannot.</summary>
    [Parameter, EditorRequired]
    public SessionActions Actions { get; set; } = null!;

    /// <summary>The selected track, or null when none is selected.</summary>
    [Parameter]
    public Track? Selected { get; set; }

    private string Heading => Selected is null ? "Sends" : $"Sends of {Selected.Name}";

    private IReadOnlyList<Track> Buses => [.. Document.Tracks.Where(track => track.Kind == TrackKind.Bus)];

    private IReadOnlyList<Send> Sends => Selected is { Kind: not TrackKind.Bus } track ? [.. track.Sends] : [];

    // Only buses the track does not already send to are offered, so a duplicate is never a choice.
    private IReadOnlyList<MenuItem> AddItems
    {
        get
        {
            if (Selected is not { Kind: not TrackKind.Bus } track)
            {
                return [];
            }
            return [.. Buses
                .Where(bus => bus.Id != track.Id && track.Sends.All(send => send.Bus != bus.Id))
                .Select(bus => new MenuItem(bus.Id.Value, bus.Name, string.Empty))];
        }
    }

    private string BusName(TrackId bus) => Document.Tracks.FirstOrDefault(track => track.Id == bus)?.Name ?? bus.Value;

    private Task AddAsync(MenuItem item)
    {
        return Selected is { } track ? Actions.Sends.Add(track.Id, TrackId.From(item.Id)) : Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        // The first render does not ask ShouldRender, so what it drew is remembered here; without it the next change would draw again for nothing.
        if (firstRender)
        {
            _shown = Key();
        }
    }

    /// <inheritdoc />
    protected override bool ShouldRender()
    {
        string now = Key();
        bool same = now == _shown;
        _shown = now;
        return !same;
    }

    private string Key()
    {
        string sends = string.Join(',', Sends.Select(send => $"{send.Bus.Value}:{send.Level}:{send.Pre}:{send.Muted}"));
        string buses = string.Join(',', Buses.Select(bus => $"{bus.Id.Value}:{bus.Name}"));
        return $"{Selected?.Id.Value}|{Selected?.Name}|{Selected?.Kind}|{sends}|{buses}|{Actions.CanEdit}|{Actions.Reason}";
    }
}
