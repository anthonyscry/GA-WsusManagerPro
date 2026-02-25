param(
    [Parameter(Mandatory=$true)]
    [string]$CurrentResultsPath,

    [Parameter(Mandatory=$true)]
    [string]$BaselinePath,

    [Parameter(Mandatory=$false)]
    [double]$ThresholdPercent = 10.0
)

$ErrorActionPreference = "Stop"

# Resolve current benchmark CSV files
$currentFiles = @()

if (Test-Path -PathType Container $CurrentResultsPath) {
    $searchPatterns = @(
        "*-report.csv",
        "*-report-*.csv",
        "*-measurements.csv",
        "*-measurements-*.csv"
    )

    foreach ($pattern in $searchPatterns) {
        $currentFiles += Get-ChildItem -Path $CurrentResultsPath -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue
    }

    if ($currentFiles.Count -eq 0) {
        Write-Host "ERROR: No benchmark result CSV files found under: $CurrentResultsPath" -ForegroundColor Red
        exit 1
    }

    $currentFiles = $currentFiles | Sort-Object LastWriteTime -Descending | Select-Object -Unique
    Write-Host "Found $($currentFiles.Count) benchmark result file(s) under $CurrentResultsPath"
} elseif (Test-Path $CurrentResultsPath) {
    $currentFiles = @((Get-Item $CurrentResultsPath))
} else {
    Write-Host "ERROR: Current results path not found: $CurrentResultsPath" -ForegroundColor Red
    exit 1
}

$current = @()
foreach ($file in $currentFiles) {
    $current += Import-Csv -Path $file.FullName
}

# Import baseline CSV file
if (-not (Test-Path $BaselinePath)) {
    Write-Host "ERROR: Baseline file not found: $BaselinePath" -ForegroundColor Red
    exit 1
}

$baseline = Import-Csv $BaselinePath

if ($current.Count -eq 0) {
    Write-Host "ERROR: Current results file(s) contain no benchmark rows." -ForegroundColor Red
    exit 1
}

if ($baseline.Count -eq 0) {
    Write-Host "ERROR: Baseline file contains no benchmark rows." -ForegroundColor Red
    exit 1
}

function Get-BenchmarkNameField {
    param([psobject]$Row)

    if ($Row.PSObject.Properties.Match('Benchmark').Count -gt 0) { return 'Benchmark' }
    if ($Row.PSObject.Properties.Match('Method').Count -gt 0) { return 'Method' }
    if ($Row.PSObject.Properties.Match('Target_Method').Count -gt 0) { return 'Target_Method' }

    return $null
}

function Get-BenchmarkName {
    param([psobject]$Row)

    if ($Row.PSObject.Properties.Match('Benchmark').Count -gt 0) { return [string]$Row.Benchmark }
    if ($Row.PSObject.Properties.Match('Method').Count -gt 0) { return [string]$Row.Method }
    if ($Row.PSObject.Properties.Match('Target_Method').Count -gt 0) { return [string]$Row.Target_Method }

    return $null
}

function Get-BenchmarkMean {
    param([psobject]$Row)

    $fields = @('Mean', 'Value', 'Measurement_Value')
    foreach ($field in $fields) {
        if ($Row.PSObject.Properties.Match($field).Count -eq 0) { continue }
        $value = [string]$Row.$field
        if (-not [string]::IsNullOrWhiteSpace($value)) { return $value }
    }

    return $null
}

function Convert-ToDouble {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    if ($Value -match '^\s*(N/?A|Not Available|n/a)\b') { return $null }

    # Keep digits, decimal separator and minus sign only.
    $normalized = ($Value -replace '[^0-9\.-]', '').Trim()
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $null }

    $result = 0.0
    if (-not [double]::TryParse($normalized, [Globalization.NumberStyles]::Any, [Globalization.CultureInfo]::InvariantCulture, [ref]$result)) {
        return $null
    }

    return $result
}

$baselineNameField = Get-BenchmarkNameField $baseline[0]

if (-not $baselineNameField) {
    Write-Host "ERROR: Could not determine benchmark name column from baseline results" -ForegroundColor Red
    exit 1
}

# Group by benchmark name (extract base name before parameters)
$currentGrouped = $current | ForEach-Object {
    $name = Get-BenchmarkName $_
    if ([string]::IsNullOrWhiteSpace($name)) {
        return
    }

    $_ | Add-Member -NotePropertyName __BenchmarkName -NotePropertyValue ($name -split '\s*\|\s*')[0].Trim() -Force
    $_
} | Group-Object -Property __BenchmarkName

$baselineGrouped = $baseline | ForEach-Object {
    $_ | Add-Member -NotePropertyName __BenchmarkName -NotePropertyValue ("$($_.$baselineNameField)" -split '\s*\|\s*')[0].Trim() -Force
    $_
} | Group-Object -Property __BenchmarkName

$regressions = @()
$comparisons = @()

foreach ($group in $currentGrouped) {
    $name = $group.Name
    $baselineGroup = $baselineGrouped | Where-Object { $_.Name -eq $name }

    if (-not $baselineGroup) {
        Write-Host "WARNING: No baseline found for '$name' - skipping comparison" -ForegroundColor Yellow
        continue
    }

    # Get mean values (handle "us" vs "ms" vs "ns" units)
    $currentMeanRaw = Get-BenchmarkMean $group.Group[0]
    $baselineMeanRaw = Get-BenchmarkMean $baselineGroup.Group[0]

    $currentMean = Convert-ToDouble -Value $currentMeanRaw
    $baselineMean = Convert-ToDouble -Value $baselineMeanRaw

    if ($null -eq $currentMean -or $null -eq $baselineMean) {
        Write-Host "WARNING: Could not parse numeric values for '$name' (Current: '$currentMeanRaw', Baseline: '$baselineMeanRaw')" -ForegroundColor Yellow
        continue
    }

    # Calculate percent change
    if ($baselineMean -eq 0) {
        $percentChange = 0
    } else {
        $percentChange = (($currentMean - $baselineMean) / $baselineMean) * 100
    }

        $comparison = [PSCustomObject]@{
            Benchmark = $name
            Baseline = "$baselineMeanRaw"
            Current = "$currentMeanRaw"
            Change = $percentChange.ToString("F2")
            Status = if ($percentChange -gt $ThresholdPercent) { "REGRESSION" } else { "OK" }
        }

    $comparisons += $comparison

    if ($percentChange -gt $ThresholdPercent) {
        $regressions += $comparison
        Write-Host ("REGRESSION: '{0}' degraded by {1:F2}% (threshold: {2:F2}%)" -f $name, $percentChange, $ThresholdPercent) -ForegroundColor Red
    } else {
        Write-Host ("OK: '{0}' changed by {1:F2}%" -f $name, $percentChange) -ForegroundColor Green
    }
}

# Output summary
Write-Host "`n=== Performance Regression Summary ===" -ForegroundColor Cyan
Write-Host "Benchmarks compared: $($comparisons.Count)"
Write-Host "Regressions found: $($regressions.Count)"
Write-Host "Threshold: $ThresholdPercent%`n"

# Exit with error if regressions detected
if ($regressions.Count -gt 0) {
    Write-Host "`nRegressions detected:" -ForegroundColor Red
    $regressions | Format-Table -AutoSize
    exit 1
} else {
    Write-Host "No performance regressions detected!" -ForegroundColor Green
    exit 0
}
