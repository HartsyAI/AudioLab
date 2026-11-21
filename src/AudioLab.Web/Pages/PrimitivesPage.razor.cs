using AudioLab.Web.Components;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Pages;

/// <summary>Renders each catalogue control that takes its behaviour from the primitives library, for the O10 evaluation.</summary>
public sealed partial class PrimitivesPage
{
    private readonly AlMenuItem[] _trackItems =
    [
        new("rename", "Rename", "F2"),
        new("duplicate", "Duplicate", "Ctrl+D"),
        new("delete", "Delete track", "Del", Destructive: true),
    ];

    private readonly string[] _grids = ["Bars", "1/4 note", "1/8 note", "1/16 note"];

    private readonly string[] _models = ["ACE-Step", "Kokoro", "Whisper small", "Whisper large", "Demucs", "Piper", "Parakeet"];

    private readonly string[] _tabs = ["Mixer", "Clip editor", "Effects"];

    private bool _confirmOpen;

    private bool _formOpen;

    private string? _grid = "Bars";

    private string? _model;

    [Inject]
    private AlToastService Toasts { get; set; } = default!;

    private string LastAction { get; set; } = "none";

    private void OpenConfirm()
    {
        _confirmOpen = true;
    }

    private void OpenForm()
    {
        _formOpen = true;
    }

    private void Deleted()
    {
        LastAction = "project deleted";
    }

    private void Renamed()
    {
        LastAction = "project renamed";
    }

    private void Chose(AlMenuItem item)
    {
        LastAction = item.Id;
    }

    private void ShowSaved()
    {
        Toasts.Show(AlToastKind.Success, "Project saved", () =>
        {
            LastAction = "undo save";
            StateHasChanged();
            return Task.CompletedTask;
        });
    }

    private void ShowFailed()
    {
        Toasts.Show(AlToastKind.Error, "Export failed: the output folder is not writable");
    }
}
