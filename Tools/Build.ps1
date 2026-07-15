param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Source\1.6\LCAnomalyOrdeals\LCAnomalyOrdeals.csproj"
$candidates = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
)
$msbuild = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild was not found. Install Visual Studio or Visual Studio Build Tools."
}

& $msbuild $project /t:Rebuild "/p:Configuration=$Configuration" /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
