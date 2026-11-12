param(
    [string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $projectRoot 'Temp/AntiCheatChecks'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$suites = @{
    DisconnectNotice = @('Assets/Scripts/DisconnectNoticeState.cs')
    BuildIntegrity = @('Assets/Scripts/BuildIntegrity.cs')
    MovementObserver = @('Assets/Scripts/HorizontalMovementObservation.cs', 'Assets/Scripts/ApprovedMovementLimits.cs')
    ServerGameplayValidation = @('Assets/Scripts/ServerGameplayValidation.cs')
}
foreach ($suite in @('DisconnectNotice', 'BuildIntegrity', 'MovementObserver', 'ServerGameplayValidation')) {
    $testSources = @((Join-Path $projectRoot "Tests/${suite}Checks.cs"))
    $testSources += $suites[$suite] | ForEach-Object { Join-Path $projectRoot $_ }
    $testExecutable = Join-Path $outputDirectory ($suite + 'Checks.exe')
    & "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" `
        -nologo -target:exe -nostdlib+ -langversion:9.0 "-out:$testExecutable" `
        "-r:$UnityData/MonoBleedingEdge/lib/mono/4.8-api/mscorlib.dll" `
        "-r:$UnityData/MonoBleedingEdge/lib/mono/4.8-api/System.dll" `
        "-r:$UnityData/MonoBleedingEdge/lib/mono/4.8-api/System.Core.dll" $testSources
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $suite" }
    & "$UnityData/MonoBleedingEdge/bin/mono.exe" $testExecutable
    if ($LASTEXITCODE -ne 0) { throw "Checks failed: $suite" }
}
Write-Output 'All four deterministic suites passed. Unity/FishNet/build/multiplayer tests are separate.'
