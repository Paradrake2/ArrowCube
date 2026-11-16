param([string]$ReportDirectory)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $ReportDirectory) { $ReportDirectory=Join-Path $projectRoot 'TestResults\AdvancedDifficulty' }
$rows=@(Import-Csv (Join-Path $ReportDirectory 'FinalVerified\benchmark.csv'))
if($rows.Count -lt 1000) { throw 'The full benchmark has not completed.' }
if((Get-Item (Join-Path $ReportDirectory 'FinalVerified\errors.txt')).Length -ne 0) { throw 'The benchmark contains errors.' }
function Average($group,[string]$field) { return ($group | ForEach-Object { [double]$_.$field } | Measure-Object -Average).Average }
function Number([double]$value,[int]$digits=2) { return $value.ToString(('F'+$digits),[Globalization.CultureInfo]::InvariantCulture) }
function Percentile($group,[string]$field,[double]$fraction) { $values=@($group | ForEach-Object { [double]$_.$field } | Sort-Object); return $values[[int][Math]::Floor(($values.Count-1)*$fraction)] }
$lines=New-Object 'System.Collections.Generic.List[string]'
$lines.Add('# Advanced difficulty verification')
$lines.Add('')
$lines.Add('Verified 2026-11-15 with Unity 6000.6.7f1 and the independent .NET 10 probe.')
$lines.Add('')
$lines.Add('The original generator was benchmarked before changing construction. The final paired run uses 70 seeds, five presets and all three exit modes: **1,050 new puzzles and 1,050 matched version-2 puzzles**, with zero failed recipes or accepted solution-validation failures. Each pair uses the same seed, grid, path-length/count bounds, coverage bounds and exit flags. Difficulty analysis and actual gameplay simulations determine the measurements.')
$lines.Add('')
$lines.Add('## Paired results')
$lines.Add('')
$lines.Add('| Preset | Mean score | Score P10 / P50 / P90 | Edge failures/removal: old -> new | Change | Mean paths: old -> new | Mean / P95 generation ms | Within target tolerance |')
$lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach($preset in @('Easy','Normal','Hard','Expert','Extreme')) {
    $group=@($rows | Where-Object preset -eq $preset)
    $old=Average $group 'baseline_edge_failures_per_removal'; $new=Average $group 'edge_failures_per_removal'
    $hits=@($group | Where-Object target_reached -eq 'True').Count/$group.Count
    $lines.Add('| '+$preset+' | '+(Number (Average $group 'score'))+' | '+(Number (Percentile $group 'score' .1))+' / '+(Number (Percentile $group 'score' .5))+' / '+(Number (Percentile $group 'score' .9))+' | '+(Number $old)+' -> '+(Number $new)+' | '+(Number (100*($new/$old-1)) 1)+'% | '+(Number (Average $group 'baseline_paths') 1)+' -> '+(Number (Average $group 'paths') 1)+' | '+(Number (Average $group 'generation_ms') 1)+' / '+(Number (Percentile $group 'generation_ms' .95) 1)+' | '+(Number (100*$hits) 1)+'% |')
}
$lines.Add('')
$lines.Add('All presets have 210 samples. Higher failures/removal means the blind strategy makes more mistakes. Scores are engineering heuristics, not validated human difficulty ratings. New generation time includes candidate analysis and optimization; original generation did not perform that work. Measurements are local CPU batch timings, not a mobile performance guarantee.')
$lines.Add('')
$lines.Add('## Control for path count')
$lines.Add('')
$lines.Add('Normalize edge mistakes as `2 * failedAttempts / (N * (N - 1))`. This controls for the number of remaining choices. Hard through Extreme improve resistance while averaging fewer arrows than their matched baselines.')
$lines.Add('')
$lines.Add('| Preset | Normalized edge mistakes: old -> new | Change |')
$lines.Add('| --- | ---: | ---: |')
foreach($preset in @('Easy','Normal','Hard','Expert','Extreme')) {
    $group=@($rows | Where-Object preset -eq $preset)
    $old=Average $group 'baseline_normalized_edge_mistakes'; $new=Average $group 'normalized_edge_mistakes'
    $lines.Add('| '+$preset+' | '+(Number $old 4)+' -> '+(Number $new 4)+' | '+(Number (100*($new/$old-1)) 1)+'% |')
}
$lines.Add('')
$lines.Add('## Other blind strategies')
$lines.Add('')
$lines.Add('Failures per successful removal. Random values average the same seeded alternative-order count for each member of a pair.')
$lines.Add('')
$lines.Add('| Preset | Projected exit distance: old -> new | Outermost body layer: old -> new | Random: old -> new |')
$lines.Add('| --- | ---: | ---: | ---: |')
foreach($preset in @('Easy','Normal','Hard','Expert','Extreme')) {
    $group=@($rows | Where-Object preset -eq $preset)
    $lines.Add('| '+$preset+' | '+(Number (Average $group 'baseline_exit_failures_per_removal'))+' -> '+(Number (Average $group 'exit_failures_per_removal'))+' | '+(Number (Average $group 'baseline_outer_failures_per_removal'))+' -> '+(Number (Average $group 'outer_failures_per_removal'))+' | '+(Number (Average $group 'baseline_random_failures_per_removal'))+' -> '+(Number (Average $group 'random_failures_per_removal'))+' |')
}
$lines.Add('')
$lines.Add('These solvers do not see the stored solution or hidden removability. They retry failed paths only after a state change. Their specific repeated-attempt behavior is not a claim about every human player. All safe removal orders remain valid under the unchanged monotonic gameplay rules.')
$lines.Add('')
$lines.Add('## Regression evidence')
$lines.Add('')
foreach($platform in @('EditMode','PlayMode')) {
    [xml]$xml=Get-Content (Join-Path $ReportDirectory ('UnityFinal\results-'+$platform+'.xml'))
    $run=$xml.'test-run'; if($run.result -ne 'Passed') { throw ($platform+' tests are not passing.') }
    $lines.Add('- '+$platform+': **'+$run.passed+'/'+$run.total+' passed**.')
}
$original=@(Import-Csv (Join-Path $ReportDirectory 'Baseline\baseline.csv'))
$rechecked=@(Import-Csv (Join-Path $ReportDirectory 'BaselineFinal\baseline.csv'))
$differences=@(Compare-Object ($original.fingerprint | Sort-Object) ($rechecked.fingerprint | Sort-Object))
if($differences.Count -ne 0 -or $original.Count -ne 1008) { throw 'Legacy fingerprints changed.' }
$lines.Add('- **1,008 original fingerprints rechecked; zero differences.** Original and finalized-analyzer baselines are retained separately.')
$lines.Add('- Unity tests cover version-1/version-2 compatibility, a version-3 .NET golden, all three exit modes, all five placement strategies, exact cross-face blocker analysis, deterministic scores, target misses, bounded search, discarded mutations, independent Custom sliders, master/reset behavior, background cancellation/replacement, JSON export and existing desktop/touch controls.')
$lines.Add('- The original 1,008-puzzle stress suite and larger-grid regression tests also pass in Unity.')
$lines.Add('')
$lines.Add('## Limits and reproduction')
$lines.Add('')
$lines.Add('The initial Extreme target of 80-100 is not reliably reached. The UI exposes the actual measured score and an explicit target-miss warning; no failed target is reported as achieved. Human difficulty calibration and large-grid/mobile profiling remain empirical follow-up work. Difficulty never reduces player attempts or changes movement/exit rules.')
$lines.Add('')
$lines.Add('Full data: [paired benchmark](FinalVerified/benchmark.csv), [final original baseline](BaselineFinal/baseline.csv), [original pre-change baseline](Baseline/baseline.csv), and the empty [benchmark error log](FinalVerified/errors.txt). Earlier tuning folders are diagnostic runs, not additional final samples. See the [implementation and reproduction guide](../../Documentation/Advanced-Procedural-Difficulty.md).')
[IO.File]::WriteAllLines((Join-Path $ReportDirectory 'Summary.md'),$lines,(New-Object Text.UTF8Encoding($false)))
Write-Output ($lines -join [Environment]::NewLine)
