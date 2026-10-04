$ErrorActionPreference = 'Stop'
$spriteRoot = Join-Path $PSScriptRoot '../src/MonoHome.Android/Resources/drawable'
foreach ($species in 1..493) {
    $normal = Join-Path $spriteRoot "a_$species.png"
    $shiny = Join-Path $spriteRoot "a_${species}s.png"
    if (!(Test-Path -LiteralPath $shiny)) { throw "Missing shiny sprite: $species" }
    if ((Get-FileHash -LiteralPath $normal).Hash -eq (Get-FileHash -LiteralPath $shiny).Hash) {
        throw "Shiny sprite equals normal sprite: $species"
    }
}
if (!(Test-Path -LiteralPath (Join-Path $spriteRoot 'a_egg.png'))) { throw 'Missing egg sprite' }
'PASS: all 493 Gen 1–4 shiny sprites exist and differ from normal sprites; egg sprite exists.'
