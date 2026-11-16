param(
    [string]$UnityPath,
    [string]$OutputDirectory,
    [ValidateSet('EditMode', 'PlayMode', 'All')][string]$TestPlatform = 'EditMode',
    [string]$ArtifactDirectory,
    [string]$TestFilter
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $UnityPath) {
    $versionLine = Get-Content (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1
    $version = ($versionLine -split ': ')[1]
    $UnityPath = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'Temp\UnityValidation' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

# Each invocation uses a fresh project so removed source files cannot survive from an earlier run.
# A separate project avoids touching the user's open scene or competing for the Editor project lock.
# Keep package resource paths below Windows' legacy import path limit.
$validationRoot = Join-Path $OutputDirectory ('Project-' + [Guid]::NewGuid().ToString('N').Substring(0, 12))
New-Item -ItemType Directory -Path (Join-Path $validationRoot 'Assets'), (Join-Path $validationRoot 'Packages'), (Join-Path $validationRoot 'ProjectSettings') -Force | Out-Null
foreach ($folder in @('Scripts', 'Editor', 'Tests', 'Scenes', 'Resources')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "Assets\$folder") -Destination (Join-Path $validationRoot 'Assets') -Recurse
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') -Destination (Join-Path $validationRoot 'ProjectSettings')
Copy-Item -LiteralPath (Join-Path $projectRoot 'ProjectSettings\EditorBuildSettings.asset') -Destination (Join-Path $validationRoot 'ProjectSettings')
$manifest = @{
    dependencies = @{
        'com.unity.test-framework' = '1.8.0'
        'com.unity.modules.imgui' = '1.0.0'
        'com.unity.modules.jsonserialize' = '1.0.0'
        'com.unity.modules.physics' = '1.0.0'
        'com.unity.modules.imageconversion' = '1.0.0'
        'com.unity.inputsystem' = '1.20.1'
        'com.unity.ugui' = '2.6.0'
    }
    testables = @('com.unity.inputsystem')
} | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $validationRoot 'Packages\manifest.json'), $manifest, (New-Object Text.UTF8Encoding($false)))
$platforms = if ($TestPlatform -eq 'All') { @('EditMode', 'PlayMode') } else { @($TestPlatform) }
foreach ($platform in $platforms) {
    $resultsPath = Join-Path $validationRoot "results-$platform.xml"
    $logPath = Join-Path $validationRoot "editor-$platform.log"
    $arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $validationRoot + '"'),
        '-runTests', '-testPlatform', $platform, '-testResults', ('"' + $resultsPath + '"'),
        '-logFile', ('"' + $logPath + '"'))
    # Package test fixtures are enabled for simulated devices; run only the game's assemblies.
    $assemblyName = if ($platform -eq 'EditMode') { 'ArrowCube.Topology.Tests' } else { 'ArrowCube.Movement.PlayMode.Tests' }
    $arguments += @('-assemblyNames', $assemblyName)
    if ($TestFilter) { $arguments += @('-testFilter', $TestFilter) }
    Write-Output "Running Unity $platform tests in $validationRoot"
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
    if (-not (Test-Path -LiteralPath $resultsPath)) {
        Get-Content -LiteralPath $logPath -Tail 70 -ErrorAction SilentlyContinue
        throw "Unity did not produce test results (exit $($process.ExitCode)). Log: $logPath"
    }
    [xml]$results = Get-Content -LiteralPath $resultsPath
    $run = $results.'test-run'
    Write-Output "${platform}: $($run.result); total=$($run.total); passed=$($run.passed); failed=$($run.failed); skipped=$($run.skipped)"
    Write-Output "Results: $resultsPath"
    Write-Output "Log: $logPath"
    if ($process.ExitCode -ne 0 -or $run.result -ne 'Passed' -or [int]$run.total -eq 0) {
        $results.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object { Write-Output $_.OuterXml }
        throw "$platform validation failed."
    }
    if ($ArtifactDirectory) {
        New-Item -ItemType Directory -Path $ArtifactDirectory -Force | Out-Null
        Copy-Item -LiteralPath $resultsPath, $logPath -Destination $ArtifactDirectory -Force
        $reportFolder = Join-Path $validationRoot 'TestResults'
        if (Test-Path -LiteralPath $reportFolder) {
            Get-ChildItem -LiteralPath $reportFolder -File | ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination $ArtifactDirectory -Force
            }
        }
    }
}
