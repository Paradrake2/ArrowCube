param([string]$ReportDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $ReportDirectory) { $ReportDirectory=Join-Path $root 'TestResults\MultiBlocker' }
$rows=@(Import-Csv (Join-Path $ReportDirectory 'FinalVerified\benchmark.csv'))
$control=@(Import-Csv (Join-Path $ReportDirectory 'ControlVerified\benchmark.csv'))
if($rows.Count -ne 1050 -or $control.Count -ne 300) { throw 'The final benchmark/control run is incomplete.' }
foreach($folder in @('FinalVerified','ControlVerified')) { if((Get-Item (Join-Path $ReportDirectory ($folder+'\errors.txt'))).Length -ne 0) { throw 'Benchmark errors must be resolved.' } }
function Avg($g,$field) { return ($g|ForEach-Object { [double]$_.$field }|Measure-Object -Average).Average }
function Num([double]$x,[int]$digits=2) { return $x.ToString(('F'+$digits),[Globalization.CultureInfo]::InvariantCulture) }
function Pair($g,$field,[double]$scale=1,[int]$digits=2) { return (Num ((Avg $g ('old_'+$field))*$scale) $digits)+' -> '+(Num ((Avg $g ('new_'+$field))*$scale) $digits) }
function DeltaInterval($g) {
    $d=@($g|Group-Object seed|ForEach-Object { $v=@($_.Group|ForEach-Object { [double]$_.new_edge_failures-[double]$_.old_edge_failures }); ($v|Measure-Object -Average).Average })
    $mean=($d|Measure-Object -Average).Average; $squares=0; foreach($v in $d) { $squares+=($v-$mean)*($v-$mean) }
    $margin=1.96*[Math]::Sqrt($squares/[Math]::Max(1,$d.Count-1)/$d.Count)
    return (Num ($mean-$margin))+' to '+(Num ($mean+$margin))
}
$lines=New-Object 'System.Collections.Generic.List[string]'
$lines.Add('# Independent multi-blocker verification')
$lines.Add('')
$lines.Add('Verified 2026-11-15. Unity 6000.6.7f1; independent .NET 10 probe. All relationships arise from unchanged movement/collision rules.')
$lines.Add('')
$lines.Add('## Full preset comparison')
$lines.Add('')
$lines.Add('**1,050 new puzzles and 1,050 original version-3 puzzles**: 70 seeds x five presets x three exit modes, on 6x6/8x8 grids. Each pair uses the same seed, path/geometry limits, coverage targets and exits. New hard-tier presets include stronger logical targets and larger deterministic search budgets. No accepted puzzle failed complete solution replay.')
$lines.Add('')
$lines.Add('| Preset | Confirmed blockers / blocked path | Independent multi-blocker paths (%) | Independent 3+ paths (%) | Dependency depth | Cross-face geometry links (%) | Initial safe (%) |')
$lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach($preset in @('Easy','Normal','Hard','Expert','Extreme')) {
    $g=@($rows|Where-Object preset -eq $preset)
    $lines.Add('| '+$preset+' | '+(Pair $g 'blockers_per_blocked')+' | '+(Pair $g 'independent_multi' 100 1)+' | '+(Pair $g 'independent_three' 100 1)+' | '+(Pair $g 'depth')+' | '+(Pair $g 'cross_geometry' 100 1)+' | '+(Pair $g 'safe' 100 1)+' |')
}
$lines.Add('')
$lines.Add('All pairs above are old -> new. Independent counts exclude redundant blockers in the same prerequisite chain. Cross-face classification uses actual contact faces and blocker geometry; an unrelated cross-face target tail does not qualify.')
$lines.Add('')
$lines.Add('| Preset | Edge mistakes / removal | Change | Normalized edge mistakes | Paired delta 95% interval | Mean paths | Coverage (%) | Mean generation ms |')
$lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach($preset in @('Easy','Normal','Hard','Expert','Extreme')) {
    $g=@($rows|Where-Object preset -eq $preset); $old=Avg $g 'old_edge_failures'; $new=Avg $g 'new_edge_failures'
    $lines.Add('| '+$preset+' | '+(Pair $g 'edge_failures')+' | '+(Num (100*($new/$old-1)) 1)+'% | '+(Pair $g 'normalized_edge' 1 4)+' | '+(DeltaInterval $g)+' | '+(Pair $g 'paths' 1 1)+' | '+(Pair $g 'coverage' 100 1)+' | '+(Pair $g 'ms' 1 1)+' |')
}
$lines.Add('')
$lines.Add('Higher mistakes mean lower effectiveness for the blind edge-first strategy. The exploratory 95% interval uses 70 seed-level mean differences, averaging the three exit modes within each seed before calculating mean +/- 1.96 standard errors. These are deterministic simulation measurements, not human-playtest validation. Normalized mistakes are `2 * failures / (N * (N - 1))`, controlling for the number of arrow choices. Timing includes validation, graph proofs, heuristic solves and optimization; local CPU timings are not a mobile-device benchmark.')
$lines.Add('')
$lines.Add('## Feature-toggle control')
$lines.Add('')
$lines.Add('A separate **300-pair** comparison holds the new logical settings and search budgets fixed, then toggles only multi-blocker construction. This prevents attributing all full-release gains to the toggle alone.')
$lines.Add('')
$lines.Add('| Preset | Independent multi-blocker paths (%): off -> on | Edge mistakes / removal: off -> on |')
$lines.Add('| --- | ---: | ---: |')
foreach($preset in @('Easy','Normal','Hard','Expert','Extreme')) {
    $g=@($control|Where-Object preset -eq $preset)
    $lines.Add('| '+$preset+' | '+(Pair $g 'independent_multi' 100 1)+' | '+(Pair $g 'edge_failures')+' |')
}
$lines.Add('')
$lines.Add('The isolated toggle has mixed strategy effects, including some Expert/Extreme regressions, despite improved dependency distributions in several cases. The full version-4 preset/search package improves resistance against version 3; the data does **not** establish that enabling multi-blockers always makes every puzzle harder. Safe removals still commute. No universal human-difficulty claim is made.')
$lines.Add('')
$lines.Add('## Solvability, compatibility and tests')
$lines.Add('')
$legacy=@(Import-Csv (Join-Path $root 'TestResults\AdvancedDifficulty\FinalVerified\benchmark.csv')); $map=@{}
foreach($r in $legacy) { $map[$r.preset+'/'+$r.mode+'/'+$r.seed]=$r.fingerprint }
$changed=@($rows|Where-Object { $map[$_.preset+'/'+$_.mode+'/'+$_.seed] -ne $_.old_fingerprint })
if($changed.Count -gt 0) { throw 'Version-3 reference fingerprints changed.' }
$lines.Add('- **1,050/1,050 version-3 reference fingerprints unchanged.** Versions 1 and 2 retain their golden tests and original code paths.')
foreach($platform in @('EditMode','PlayMode')) {
    [xml]$xml=Get-Content (Join-Path $ReportDirectory ('UnityFinal\results-'+$platform+'.xml')); $run=$xml.'test-run'
    if($run.result -ne 'Passed') { throw ($platform+' did not pass.') }
    $lines.Add('- '+$platform+': **'+$run.passed+'/'+$run.total+' passed**.')
    if($platform -eq 'EditMode') {
        foreach($test in $xml.SelectNodes('//test-case[contains(@name,"LargerCubesUse")]')) { $lines.Add('  - '+$test.name+': '+$test.duration+' seconds including generation and explicit replay.') }
    }
}
$lines.Add('- Tests cover full-body cells/segments, zero through three independent blockers, bounded 5/14-blocker verification, cross-face contacts, early gates, all exit modes, redundant chains, cycles, deep snapshots, disabled mode, cache invalidation, budgets, determinism, cross-runtime goldens, discarded mutations, independent Custom controls, debug click selection, JSON/DOT exports and existing desktop/touch controls.')
$accepted=($rows|Measure-Object targeted_accepted -Sum).Sum
$lines.Add('- Accepted targeted replacements/insertions in the final corpus: **'+$accepted+'**. Default replacement mode preserves path count in that pass; ordinary construction can produce different path counts within the same bounds.')
$lines.Add('')
$lines.Add('## Limits and data')
$lines.Add('')
$lines.Add('Preset blocker fractions/averages remain soft targets and are often missed, especially at Expert/Extreme. The UI reports actual values and target-miss warnings. Subset enumeration is exhaustive only through three blockers; larger sets have singleton/zero/full proofs under fixed-route rules. Independent width is exact through twelve blockers, then a verified conservative lower bound. Future gameplay with selectable alternate routes requires re-evaluating mandatory classifications.')
$lines.Add('')
$lines.Add('Data: [final paired CSV](FinalVerified/benchmark.csv), [same-budget toggle CSV](ControlVerified/benchmark.csv), [empty final error log](FinalVerified/errors.txt), [empty control error log](ControlVerified/errors.txt), and [implementation guide](../../Documentation/MultiBlocker-Dependencies.md). Earlier calibration folders are diagnostic runs, not extra final samples. Recreate this summary with `Tools/SummarizeMultiBlockerBenchmark.ps1`.')
[IO.File]::WriteAllLines((Join-Path $ReportDirectory 'Summary.md'),$lines,(New-Object Text.UTF8Encoding($false)))
Write-Output ($lines -join [Environment]::NewLine)
