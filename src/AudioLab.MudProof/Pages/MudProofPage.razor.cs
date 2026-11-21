namespace AudioLab.MudProof.Pages;

/// <summary>Renders one MudBlazor-backed dialog beside plain elements, to measure what MudBlazor's global stylesheet changes.</summary>
public sealed partial class MudProofPage
{
    private bool _open;

    private void OpenDialog()
    {
        _open = true;
    }
}
