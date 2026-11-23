using AudioLab.Core.Api;
using AudioLab.Core.Realtime;
using AudioLab.Web.Components;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AudioLab.Web.Pages;

/// <summary>The project list: create, open, rename and delete, over the project routes and, for a rename, the hub.</summary>
public sealed partial class ProjectListPage : IDisposable
{
    private const string DefaultName = "Untitled project";

    // Ends the requests that are still running when the page goes, so none of them answers to a page that is not there.
    private readonly CancellationTokenSource _page = new();

    private IReadOnlyList<ProjectSummary> _projects = [];

    private int _refused;

    private bool _loading = true;

    private string? _failure;

    private ProjectDialog _mode;

    private ProjectSummary? _target;

    private string _name = string.Empty;

    private string? _error;

    private bool _busy;

    // The request of the open dialog; Cancel and Escape end it.
    private CancellationTokenSource? _request;

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

    private bool NameIsBlank => string.IsNullOrWhiteSpace(_name);

    private bool CannotConfirmName => NameIsBlank || _busy;

    private string? NameReason => _busy ? BusyReason : NameIsBlank ? "Enter a name for the project." : null;

    private string? BusyReason => _busy ? "Waiting for the server." : null;

    // The page has one primary button: the call to action of an empty or failed list takes it from the header.
    private ButtonVariant NewProjectVariant => _loading || _failure is not null || _projects.Count == 0
        ? ButtonVariant.Default
        : ButtonVariant.Primary;

    private string RefusedText => _refused == 1
        ? "One stored file was left out because it is not a valid project."
        : $"{_refused} stored files were left out because they are not valid projects.";

    private string DeleteText => _target is null
        ? string.Empty
        : $"Deleting \"{_target.Name}\" removes it for good, for everyone who has it open. This cannot be undone.";

    /// <inheritdoc />
    public void Dispose()
    {
        _page.Cancel();
        _page.Dispose();
    }

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        return LoadAsync();
    }

    private static string RenameName(ProjectSummary project) => $"Rename {project.Name}";

    private static string DeleteName(ProjectSummary project) => $"Delete {project.Name}";

    private async Task ReloadAsync()
    {
        _loading = true;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _failure = null;
        try
        {
            await RefreshAsync();
        }
        catch (OperationCanceledException) when (_page.IsCancellationRequested)
        {
            // The page went while the list was loading: nothing is left to show it to.
        }
        catch (Exception exception)
        {
            _failure = FailureText.Of(exception);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RefreshAsync()
    {
        ProjectListing listing = await Api.ListAsync(_page.Token);
        _projects = listing.Projects;
        _refused = listing.Refused;
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

    private void BeginRename(ProjectSummary project)
    {
        _name = project.Name;
        Begin(ProjectDialog.Rename, project);
    }

    private void BeginDelete(ProjectSummary project)
    {
        Begin(ProjectDialog.Delete, project);
    }

    private void KeyDown(ProjectSummary project, KeyboardEventArgs press)
    {
        if (Shortcuts.RenameProject.Matches(press))
        {
            BeginRename(project);
        }
        else if (Shortcuts.DeleteProject.Matches(press))
        {
            BeginDelete(project);
        }
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
            _request?.Cancel();
            _mode = ProjectDialog.None;
        }
    }

    private Task CreateAsync()
    {
        return RunAsync(async token =>
        {
            ProjectSummary created = await Api.CreateAsync(_name.Trim(), token);
            Navigation.NavigateTo($"project/{created.Id}");
            return null;
        });
    }

    private Task RenameAsync()
    {
        if (_target is null)
        {
            return Task.CompletedTask;
        }
        ProjectSummary target = _target;
        string name = _name.Trim();
        return RunAsync(async token =>
        {
            CommandReply reply = await Renamer.RenameAsync(target.Id, name, token);
            if (!reply.Succeeded)
            {
                return CommandRefusal.Describe(reply);
            }
            Toasts.Show(ToastKind.Success, $"Renamed to \"{name}\".");
            await RefreshAsync();
            return null;
        });
    }

    private Task DeleteAsync()
    {
        if (_target is null)
        {
            return Task.CompletedTask;
        }
        ProjectSummary target = _target;
        return RunAsync(async token =>
        {
            await Api.DeleteAsync(target.Id, token);
            Toasts.Show(ToastKind.Success, $"Deleted \"{target.Name}\".");
            await RefreshAsync();
            return null;
        });
    }

    // One place for what every dialog does around its action. The dialog stays open and says why when the action fails, whatever the
    // failure was; Cancel and Escape end the request; and after a failed rename or delete the list is read again, because the project
    // may be gone, which is why the server refused.
    private async Task RunAsync(Func<CancellationToken, Task<string?>> action)
    {
        using CancellationTokenSource request = CancellationTokenSource.CreateLinkedTokenSource(_page.Token);
        _request = request;
        _busy = true;
        _error = null;
        bool changesAProject = _mode is ProjectDialog.Rename or ProjectDialog.Delete;
        try
        {
            _error = await action(request.Token);
            if (_error is null)
            {
                _mode = ProjectDialog.None;
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            // The user closed the dialog, or the page went: nothing failed.
        }
        catch (Exception exception)
        {
            _error = FailureText.Of(exception);
        }
        finally
        {
            _busy = false;
            _request = null;
        }
        if (_error is not null && changesAProject)
        {
            await RefreshQuietlyAsync();
        }
    }

    private async Task RefreshQuietlyAsync()
    {
        try
        {
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The dialog already says what failed; a list that cannot be read again stays as it was.
        }
    }
}
