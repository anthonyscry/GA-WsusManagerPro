[CmdletBinding()]
param(
    [string]$LogPath = "C:\WSUS\Logs",
    [switch]$VerboseOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[STEP] $Message" -ForegroundColor Cyan
}

function Write-Pass {
    param([string]$Message)
    Write-Host "[PASS] $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

Write-Step "Validating smoke-test prerequisites"

if (-not (Test-Path $LogPath)) {
    Write-Fail "Log path not found: $LogPath"
    exit 1
}

$transcriptDir = Join-Path $LogPath "Transcripts"
if (-not (Test-Path $transcriptDir)) {
    Write-Host "[INFO] Transcript directory not found yet: $transcriptDir" -ForegroundColor Yellow
} else {
    Write-Pass "Transcript directory exists: $transcriptDir"
}

Write-Step "Collecting latest WSUS manager logs"

$latestLog = Get-ChildItem -Path $LogPath -Filter "WsusManager-*.log" -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $latestLog) {
    Write-Fail "No WsusManager log files found in $LogPath"
    exit 1
}

Write-Pass "Latest log: $($latestLog.FullName)"

$content = Get-Content -Path $latestLog.FullName -Raw

Write-Step "Checking for operation telemetry markers"
if ($content -match "OperationId") {
    Write-Pass "OperationId markers found in latest log"
} else {
    Write-Host "[WARN] OperationId marker not found in latest log" -ForegroundColor Yellow
}

Write-Step "Checking fallback markers in latest log"
$fallbackMatches = Select-String -Path $latestLog.FullName -Pattern "\[FALLBACK\]" -SimpleMatch
if ($fallbackMatches) {
    Write-Host "[INFO] Fallback markers found (review if expected):" -ForegroundColor Yellow
    $fallbackMatches | Select-Object -First 20 | ForEach-Object { Write-Host "  $($_.Line)" }
} else {
    Write-Pass "No fallback markers found in latest log"
}

if (Test-Path $transcriptDir) {
    Write-Step "Inspecting transcript files"
    $latestTranscript = Get-ChildItem -Path $transcriptDir -Filter "*.log" -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $latestTranscript) {
        Write-Host "[WARN] No transcript files found in $transcriptDir" -ForegroundColor Yellow
    } else {
        Write-Pass "Latest transcript: $($latestTranscript.FullName)"
        if ($VerboseOutput) {
            Write-Host "--- Transcript Preview ---" -ForegroundColor DarkGray
            Get-Content -Path $latestTranscript.FullName -TotalCount 40 | ForEach-Object { Write-Host $_ }
        }
    }
}

Write-Host ""
Write-Host "Smoke-test helper completed. Use docs/windows-server-2019-smoke-test.md for manual operation checks." -ForegroundColor Cyan
