$ErrorActionPreference = 'Stop'
$data = Get-Content (Join-Path $PSScriptRoot '../src/MonoHome.Android/Resources/raw/move_data_zh.json') -Raw | ConvertFrom-Json
if (($data.id | Select-Object -Unique).Count -ne $data.Count) { throw 'Duplicate move IDs' }
foreach ($move in $data) {
    if ($move.id -le 826 -and [string]::IsNullOrWhiteSpace($move.description)) { throw "Missing effect: $($move.id)" }
    if ($move.id -le 719 -and $move.description -match '无法使用这个招式') { throw "Retired-move placeholder instead of effect: $($move.id)" }
    if ($null -ne $move.accuracy -and ($move.accuracy -lt 1 -or $move.accuracy -gt 100)) { throw "Invalid accuracy: $($move.id)" }
}
$bodySlam = $data | Where-Object id -eq 34
if ($bodySlam.accuracy -ne 100 -or $bodySlam.description -notmatch '麻痹') { throw 'Body Slam must describe paralysis and 100% accuracy' }
$protect = $data | Where-Object id -eq 182
if ($protect.category -ne '变化' -or $null -ne $protect.accuracy) { throw 'Protect must be a status move without an accuracy check' }
Write-Host 'MOVE DATA PASSED'
