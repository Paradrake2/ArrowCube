param([string]$UnityPath, [string]$OutputDirectory)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot '..\Temp\Stage4Validation' }
& (Join-Path $PSScriptRoot 'RunUnityTests.ps1') -UnityPath $UnityPath -OutputDirectory $OutputDirectory -TestPlatform All -ArtifactDirectory (Join-Path $PSScriptRoot '..\TestResults\Stage4')
