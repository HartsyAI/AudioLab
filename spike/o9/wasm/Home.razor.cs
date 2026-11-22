using System.Text;
using Microsoft.AspNetCore.Components;

namespace O9.Wasm;

public partial class Home
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private string Variant { get; } =
#if HI_RESAMPLER && HI_WAV && HI_FFT && HI_OTHER
        "All";
#elif HI_RESAMPLER
        "Resampler";
#elif HI_WAV
        "Wav";
#elif HI_FFT
        "Fft";
#elif HI_OTHER
        "Other";
#elif OWN
        "Own";
#else
        "None";
#endif

    private string Mode { get; } = System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled ? "JIT or interpreter" : "AOT or interpreter without dynamic code";

    private bool Running { get; set; }

    private string Status { get; set; } = "idle";

    private string Results { get; set; } = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Query().ContainsKey("run"))
        {
            await Task.Delay(500);
            await RunAsync();
        }
    }

    private Dictionary<string, string> Query() =>
        new Uri(Navigation.Uri).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : string.Empty);

    private async Task RunAsync()
    {
#if OWN
        Dictionary<string, string> query = Query();
        double seconds = query.TryGetValue("sec", out string? s) ? double.Parse(s) : 5;
        int rounds = query.TryGetValue("rounds", out string? r) ? int.Parse(r) : 5;
        Running = true;
        Status = "running";
        StateHasChanged();
        await Task.Delay(100);
        StringBuilder text = new();
        Runner runner = new(line =>
        {
            Console.WriteLine(line);
            text.AppendLine(line);
        }, seconds, rounds);
        runner.RunAll();
        Results = text.ToString();
        Status = $"done: {runner.Failures} failed assertions";
        Running = false;
        if (query.TryGetValue("save", out string? save))
        {
            using HttpClient http = new() { BaseAddress = new Uri(Navigation.BaseUri) };
            await http.PostAsync($"save/{save}", new StringContent(Results));
        }
        StateHasChanged();
#else
        Status = "done: baseline page, nothing to run";
        await Task.CompletedTask;
#endif
    }
}
