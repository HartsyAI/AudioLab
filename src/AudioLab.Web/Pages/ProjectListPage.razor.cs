using AudioLab.Core.Api;
using AudioLab.Core.Realtime;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR;

namespace AudioLab.Web.Pages;

/// <summary>The project list: create, open, rename and delete, over the project routes and, for a rename, the hub.</summary>
public sealed partial class ProjectListPage
{
    private const string DefaultName = "Untitled project";

    private IReadOnlyList<ProjectSummary> _projects = [];

    private int _refused;

    private bool _loading = true;

    private string? _failure;

    private ProjectDialog _mode;

    private ProjectSummary? _target;

    private string _name = string.Empty;

    private string? _error;

    private bool _busy;

    [Inject]
    private ProjectApi Api { get; set; } = default!;

    [Inject]
    private ProjectRenamer Renamer { get; set; } = default!;

    [Inject]
    private ToastService Toasts { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    private static IReadOnlyList<MenuItem> Actions { get; } =
    [
        new(Shortcuts.RenameProject.Id, "Rename", Shortcuts.RenameProject.Display),
        new(Shortcuts.DeleteProject.Id, "Delete", Shortcuts.DeleteProject.Display, Destructive: true),
    ];

    private bool NameIsBlank => string.IsNullOrWhiteSpace(_name);

    private string DeleteText => _target is null
        ? string.Empty
        : $"Deleting \"{_target.Name}\" removes it for good, for everyone who has it open. This cannot be undone.";

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        return LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _failure = null;
        try
        {
            ProjectListing listing = await Api.ListAsync();
            _projects = listing.Projects;
            _refused = listing.Refused;
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException)
        {
            _failure = Reason(exception);
        }
        _loading = false;
    }

    private string When(ProjectSummary project)
    {
        return $"Modified {ModifiedText.Of(project.Modified, Time.GetUtcNow(), Time.LocalTimeZone)}";
    }

    private void BeginCreate()
    {
        _name = DefaultName;
        Begin(ProjectDialog.Create, null);
    }

    private Task KeyAsync(ProjectSummary project, KeyboardEventArgs press)
    {
        if (Shortcuts.RenameProject.Matches(press))
        {
            Begin(ProjectDialog.Rename, project);
            _name = project.Name;
        }
        else if (Shortcuts.DeleteProject.Matches(press))
        {
            Begin(ProjectDialog.Delete, project);
        }
        return Task.CompletedTask;
    }

    private Task ChooseAsync(ProjectSummary project, MenuItem item)
    {
        if (item.Id == Shortcuts.RenameProject.Id)
        {
            _name = project.Name;
            Begin(ProjectDialog.Rename, project);
        }
        else if (item.Id == Shortcuts.DeleteProject.Id)
        {
            Begin(ProjectDialog.Delete, project);
        }
        return Task.CompletedTask;
    }

    private void Begin(ProjectDialog mode, ProjectSummary? target)
    {
        _target = target;
        _error = null;
        _mode = mode;
    }

    private void EndDialog(bool open)
    {
        if (!open)
        {
            _mode = ProjectDialog.None;
        }
    }

    private async Task CreateAsync()
    {
        await RunAsync(async () =>
        {
            ProjectSummary created = await Api.CreateAsync(_name.Trim());
            Navigation.NavigateTo($"project/{created.Id}");
            return null;
        });
    }

    private async Task RenameAsync()
    {
        if (_target is null)
        {
            return;
        }
        ProjectSummary target = _target;
        await RunAsync(async () =>
        {
            CommandReply reply = await Renamer.RenameAsync(target.Id, _name.Trim());
            if (!reply.Succeeded)
            {
                return CommandRefusal.Describe(reply);
            }
            Toasts.Show(ToastKind.Success, $"Renamed to \"{_name.Trim()}\".");
            await LoadAsync();
            return null;
        });
    }

    private async Task DeleteAsync()
    {
        if (_target is null)
        {
            return;
        }
        ProjectSummary target = _target;
        await RunAsync(async () =>
        {
            await Api.DeleteAsync(target.Id);
            Toasts.Show(ToastKind.Success, $"Deleted \"{target.Name}\".");
            await LoadAsync();
            return null;
        });
    }

    // One place for what every dialog does around its action: it stays open and says why when the server refuses.
    private async Task RunAsync(Func<Task<string?>> action)
    {
        _busy = true;
        _error = null;
        try
        {
            _error = await action();
            if (_error is null)
            {
                _mode = ProjectDialog.None;
            }
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or HubException)
        {
            _error = Reason(exception);
        }
        _busy = false;
    }

    private static string Reason(Exception exception)
    {
        return exception is HttpRequestException
            ? "The server did not answer. Check that AudioLab is still running."
            : exception.Message;
    }
}
