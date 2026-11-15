param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$RimWorldRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$HarmonyAssembliesDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Source\1.7\LCAnomalyOrdeals\LCAnomalyOrdeals.csproj'
$buildArguments = @('build', $project, '-c', $Configuration, '--nologo', '-v:minimal', "/p:RimWorldRoot=$RimWorldRoot")
if ($HarmonyAssembliesDir) { $buildArguments += "/p:HarmonyAssembliesDir=$HarmonyAssembliesDir" }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "RimWorld 1.7 build failed with exit code $LASTEXITCODE." }
