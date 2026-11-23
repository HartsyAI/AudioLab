<#
.SYNOPSIS
  Sets up the measurement of the engine in the page (DESIGN.md, D60): an ahead-of-time server publish, a 16-track project with effects,
  a quiet-machine check and a CPU log. The run itself is played in a browser, because the engine runs in the page.

.DESCRIPTION
  One command:   pwsh scripts/measure-engine.ps1            (Windows PowerShell 5.1 works too)

  1. Records the hardware and refuses to go on unless the machine is quiet (mean CPU under -QuietPercent for -QuietSeconds), or -Force.
  2. Publishes the server with the ahead-of-time engine (-p:AudioLabWorkerAot=true; needs the wasm-tools workload), or uses -PublishDir.
  3. Checks that the worker's runtime in the publish is the ahead-of-time one (its native module is several times the page's).
  4. Starts the published server on a scratch folder and makes the project: -Tracks audio tracks, one 30 s stereo 44.1 kHz clip each (8 different),
     the effects of -Mode (dry, fx = EQ and compressor, heavy = fx plus a reverb, a delay and a saturator on a quarter of the tracks each).
  5. Prints the address. Open it in the browser to be measured, click Play, and let it run for -Seconds, scrolling and dragging as the test needs.
     The status bar then shows the engine (ahead-of-time or interpreted), the dropouts, the longest render and the longest gap between chunks.
  6. Logs the CPU of the machine every 10 s to <OutputDir>/cpu.csv for -Seconds, and prints the mean and the peak; stop the server on Enter (-NoWait stops it at once).

  What the script cannot do is click for you or read the browser: write down which browser and version, what was dragged, and the numbers from the status bar.
#>
param(
  [int]$Tracks = 16,
  [int]$Seconds = 600,
  [ValidateSet('dry', 'fx', 'heavy')][string]$Mode = 'heavy',
  [int]$Port = 5290,
  [int]$QuietPercent = 35,
  [int]$QuietSeconds = 60,
  [string]$PublishDir,
  [string]$OutputDir = (Join-Path ([IO.Path]::GetTempPath()) 'audiolab-measure'),
  [switch]$Force,
  [switch]$NoWait
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
New-Item -ItemType Directory -Force $OutputDir | Out-Null

function Get-CpuMean([int]$count, [int]$interval) {
  $samples = (Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval $interval -MaxSamples $count).CounterSamples
  $values = $samples | ForEach-Object { [math]::Round($_.CookedValue) }
  [pscustomobject]@{ Mean = [math]::Round(($values | Measure-Object -Average).Average); Peak = ($values | Measure-Object -Maximum).Maximum; Values = $values }
}

# 1. Hardware and quiet check
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$os = Get-CimInstance Win32_OperatingSystem
$hardware = "{0}, {1} logical cores, {2:0} GB, {3} {4}, .NET {5}" -f $cpu.Name.Trim(), $cpu.NumberOfLogicalProcessors,
  ($os.TotalVisibleMemorySize / 1MB), $os.Caption, $os.BuildNumber, (dotnet --version)
Write-Host "Hardware: $hardware"
Write-Host "Checking that the machine is quiet ($QuietSeconds s, under $QuietPercent% mean)..."
$quiet = Get-CpuMean $QuietSeconds 1
Write-Host "CPU before: mean $($quiet.Mean)%, peak $($quiet.Peak)%"
if ($quiet.Mean -ge $QuietPercent -and -not $Force) {
  Write-Host "The machine is not quiet. A run now says nothing about the engine; stop the other work or pass -Force and say so in the result."
  exit 2
}

# 2 and 3. The ahead-of-time publish
if (-not $PublishDir) {
  $PublishDir = Join-Path $OutputDir 'publish'
  if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
  Write-Host "Publishing the server with the ahead-of-time engine to $PublishDir (several minutes)..."
  dotnet publish (Join-Path $root 'src/AudioLab.Server') -c Release -p:AudioLabWorkerAot=true -o $PublishDir | Out-Null
}
$workerNative = Get-ChildItem (Join-Path $PublishDir 'wwwroot/worker/_framework') -Filter 'dotnet.native*.wasm' | Select-Object -First 1
$pageNative = Get-ChildItem (Join-Path $PublishDir 'wwwroot/_framework') -Filter 'dotnet.native*.wasm' | Select-Object -First 1
Write-Host ("Worker runtime {0:0.0} MB, page runtime {1:0.0} MB" -f ($workerNative.Length / 1MB), ($pageNative.Length / 1MB))
if ($workerNative.Length -lt 2 * $pageNative.Length) {
  throw 'The worker runtime is not much larger than the page runtime: this publish is not ahead of time (is the wasm-tools workload installed?).'
}

# 4. Server and project
Add-Type -TypeDefinition @'
using System; using System.IO;
public static class ToneWav {
  public static byte[] Make(double hertz, double seconds, int rate, int seed) {
    int frames = (int)(seconds * rate); var random = new Random(seed); var ms = new MemoryStream(); var w = new BinaryWriter(ms);
    int bytes = frames * 4; w.Write(new[]{(byte)'R',(byte)'I',(byte)'F',(byte)'F'}); w.Write(36 + bytes);
    w.Write(new[]{(byte)'W',(byte)'A',(byte)'V',(byte)'E',(byte)'f',(byte)'m',(byte)'t',(byte)' '}); w.Write(16); w.Write((short)1); w.Write((short)2);
    w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)16); w.Write(new[]{(byte)'d',(byte)'a',(byte)'t',(byte)'a'}); w.Write(bytes);
    for (int n = 0; n < frames; n++) {
      double t = (double)n / rate;
      double v = 0.25 * Math.Sin(2 * Math.PI * hertz * t) + 0.12 * Math.Sin(2 * Math.PI * hertz * 2.01 * t) + 0.04 * (random.NextDouble() - 0.5);
      short s = (short)(Math.Max(-1, Math.Min(1, v)) * 32767); w.Write(s); w.Write((short)(s * 0.9));
    }
    return ms.ToArray();
  }
}
'@
$env:AudioLab__Storage__Root = Join-Path $OutputDir 'data'
$server = Start-Process -FilePath dotnet -WorkingDirectory $PublishDir -PassThru -WindowStyle Hidden `
  -ArgumentList (Join-Path $PublishDir 'AudioLab.Server.dll'), '--urls', "http://127.0.0.1:$Port"
try {
  $base = "http://127.0.0.1:$Port"
  $up = $false
  for ($i = 0; $i -lt 40 -and -not $up; $i++) { try { Invoke-RestMethod "$base/health" | Out-Null; $up = $true } catch { Start-Sleep 1 } }
  if (-not $up) { throw "The server did not answer on $base." }
  $call = 0
  function Invoke-Tool([string]$name, $arguments) {
    $script:call++
    $body = @{ jsonrpc = '2.0'; id = $script:call; method = 'tools/call'; params = @{ name = $name; arguments = $arguments } } | ConvertTo-Json -Depth 8
    $text = (Invoke-WebRequest "$base/api/mcp" -Method Post -Body $body -ContentType 'application/json' -Headers @{ Accept = 'application/json, text/event-stream' } -UseBasicParsing).Content
    $reply = ($text -split "`n" | Where-Object { $_ -like 'data: *' } | Select-Object -First 1).Substring(6) | ConvertFrom-Json
    if ($reply.error -or $reply.result.isError) { throw "$name failed: $text" }
    $reply.result.content[0].text | ConvertFrom-Json
  }
  $project = Invoke-RestMethod "$base/api/projects" -Method Post -Body (@{ name = "measure-$Mode-$Tracks" } | ConvertTo-Json) -ContentType 'application/json'
  $id = if ($project.id) { $project.id } else { $project.project.id }
  $media = 0..7 | ForEach-Object {
    $bytes = [ToneWav]::Make(110 * ($_ + 1), 30, 44100, $_)
    Invoke-RestMethod "$base/api/media?name=tone$_" -Method Post -Body $bytes -ContentType 'audio/wav'
  }
  for ($i = 0; $i -lt $Tracks; $i++) {
    $made = Invoke-Tool 'track_add' @{ project = $id; kind = 'audio'; name = "Track $($i + 1)" }
    $track = if ($made.created) { $made.created[0] } else { $made.Created[0] }
    $clip = $media[$i % 8]
    Invoke-Tool 'clip_add' @{ project = $id; track = $track; media = $clip.id; duration = $clip.duration; start = 0 } | Out-Null
    if ($Mode -ne 'dry') {
      Invoke-Tool 'effect_add' @{ project = $id; track = $track; kind = 'eq' } | Out-Null
      Invoke-Tool 'effect_add' @{ project = $id; track = $track; kind = 'compressor' } | Out-Null
      $extra = @('reverb', 'delay', 'saturation', $null)[$i % 4]
      if ($Mode -eq 'heavy' -and $extra) { Invoke-Tool 'effect_add' @{ project = $id; track = $track; kind = $extra } | Out-Null }
    }
  }

  # 5 and 6. The run
  Write-Host ""
  Write-Host "Open $base/project/$id in the browser to be measured, click Play, and let it run for $Seconds s."
  Write-Host "Logging the CPU to $(Join-Path $OutputDir 'cpu.csv') every 10 s; the hardware line above goes in the result."
  $log = Get-CpuMean ([math]::Ceiling($Seconds / 10)) 10
  $log.Values | ForEach-Object -Begin { $n = 0 } -Process { "{0},{1}" -f ($n += 10), $_ } | Set-Content (Join-Path $OutputDir 'cpu.csv')
  Write-Host "CPU during the run: mean $($log.Mean)%, peak $($log.Peak)%. Read the engine line in the status bar now, then press Enter to stop the server."
  if (-not $NoWait) { Read-Host | Out-Null }
}
finally {
  if (-not $server.HasExited) { Stop-Process -Id $server.Id -Force }
}
