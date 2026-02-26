[CmdletBinding()]
param(
    [switch]$IncludeFormat,

    [switch]$RunPackaging,

    [ValidateRange(1, 5)]
    [int]$RestoreRetryCount = 2,

    [ValidateRange(1, 30)]
    [int]$RestoreRetryDelaySeconds = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$verifyScript = Join-Path $PSScriptRoot 'verify.ps1'
if (-not (Test-Path -LiteralPath $verifyScript)) {
    throw "verify.ps1 not found at $verifyScript"
}

$pwshCommand = Get-Command pwsh -ErrorAction SilentlyContinue
$shellPath = if ($pwshCommand) {
    $pwshCommand.Source
} else {
    (Get-Command powershell -ErrorAction Stop).Source
}

$argumentList = @(
    '-NoLogo',
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', $verifyScript,
    '-Configuration', 'Release',
    '-RestoreRetryCount', $RestoreRetryCount,
    '-RestoreRetryDelaySeconds', $RestoreRetryDelaySeconds
)

if (-not $IncludeFormat) {
    $argumentList += '-SkipFormat'
}

if ($RunPackaging) {
    $argumentList += '-RunPackaging'
}

Write-Host 'Running CI-equivalent verification with local-friendly defaults...' -ForegroundColor Cyan
Write-Host ('Shell: {0}' -f $shellPath)

& $shellPath @argumentList
exit $LASTEXITCODE
