param(
    [string]$RimWorldRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [ValidateSet('English', 'ChineseSimplified')][string]$Language = 'English',
    [ValidateSet('WithoutAnomaly', 'All', 'BaseGame')][string]$DlcProfile = 'WithoutAnomaly',
    [switch]$StartupOnly,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
# Share the 1.7 family harness: isolated configuration, DLC state assertions,
# map/gameplay checks and save reload, with cleanup of only its own process.
$harness = Join-Path $RimWorldRoot 'Mods/LC Anomaly Core/Tools/1.7/RuntimeSmokeTest.ps1'
if (-not (Test-Path -LiteralPath $harness)) { throw "The LC Core 1.7 family runtime harness was not found: $harness" }
& $harness -RimWorldRoot $RimWorldRoot -Language $Language -DlcProfile $DlcProfile -StartupOnly:$StartupOnly -TimeoutSeconds $TimeoutSeconds
