param([string]$UnityPath, [string]$OutputDirectory)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot '..\Temp\Stage1Validation' }
& (Join-Path $PSScriptRoot 'RunUnityTests.ps1') -UnityPath $UnityPath -OutputDirectory $OutputDirectory -TestPlatform EditMode
