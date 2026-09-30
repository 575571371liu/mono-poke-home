# Runs the MonoHome verification suite with a workspace-local temp directory and the
# user-scoped .NET SDK. Used to prove that a change has no negative impact.
#
# Usage:  pwsh -File tools/verify.ps1
[CmdletBinding()]
param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

# The machine's dotnet host lives in Program Files but the SDK is installed per-user.
$sdkRoot = Join-Path $env:USERPROFILE '.dotnet'
if (Test-Path (Join-Path $sdkRoot 'dotnet.exe')) {
    $env:DOTNET_ROOT = $sdkRoot
    $env:PATH = "$sdkRoot;$env:PATH"
}

# Keep all scratch state inside the repository so the suite works under a confined
# file sandbox and never depends on machine-specific temp paths.
$tempRoot = Join-Path $repoRoot '.verify-tmp'
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
$env:TEMP = $tempRoot
$env:TMP = $tempRoot

$project = Join-Path $repoRoot 'src/MonoHome.Verifier/MonoHome.Verifier.csproj'
$arguments = @('run', '--project', $project, '-c', 'Release')
if ($NoBuild) { $arguments += '--no-build' }

Push-Location $repoRoot
try {
    & dotnet @arguments
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($code -ne 0) {
    Write-Host "VERIFY FAILED (exit $code)" -ForegroundColor Red
    exit $code
}
Write-Host 'VERIFY PASSED' -ForegroundColor Green
