param(
    [Parameter(Mandatory=$true)][string]$DevelopmentAssembly,
    [Parameter(Mandatory=$true)][string]$ReleaseAssembly,
    [string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$outDirectory = Join-Path $root 'Temp/AntiCheatChecks'
New-Item -ItemType Directory -Path $outDirectory -Force | Out-Null
$cecil = "$UnityData/MonoBleedingEdge/lib/mono/gac/Mono.Cecil/0.11.1.0__0738eb9f132ed756"
$exe = Join-Path $outDirectory 'AimAssemblyChecks.exe'
& "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" `
    -nologo -target:exe -nostdlib+ -langversion:9.0 "-out:$exe" `
    "-r:$UnityData/MonoBleedingEdge/lib/mono/4.8-api/mscorlib.dll" `
    "-r:$UnityData/MonoBleedingEdge/lib/mono/4.8-api/System.dll" `
    "-r:$UnityData/MonoBleedingEdge/lib/mono/4.8-api/System.Core.dll" `
    "-r:$cecil/Mono.Cecil.dll" (Join-Path $PSScriptRoot 'AimAssemblyChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Aim metadata checker compilation failed' }
$previousPath = $env:MONO_PATH
try {
    $env:MONO_PATH = $cecil
    & "$UnityData/MonoBleedingEdge/bin/mono.exe" $exe $DevelopmentAssembly $ReleaseAssembly
    if ($LASTEXITCODE -ne 0) { throw 'Aim metadata checks failed' }
} finally { $env:MONO_PATH = $previousPath }
