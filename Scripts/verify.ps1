[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$SkipFormat,

    [switch]$RunPackaging,

    [switch]$CollectCoverage,

    [ValidateRange(1, 5)]
    [int]$RestoreRetryCount = 3,

    [ValidateRange(1, 30)]
    [int]$RestoreRetryDelaySeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:StartTime = Get-Date
$script:FailedStep = ''
$script:TopFailingCommand = ''
$script:FailedLogPath = ''
$script:FailedExitCode = 0

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$script:ArtifactsRoot = Join-Path $script:RepoRoot '.ci-artifacts'
$script:LogsRoot = Join-Path $script:ArtifactsRoot 'logs'
$script:BinlogRoot = Join-Path $script:ArtifactsRoot 'binlogs'
$script:AnalyzerRoot = Join-Path $script:ArtifactsRoot 'analyzers'
$script:TestResultsRoot = Join-Path $script:ArtifactsRoot 'test-results'
$script:PublishRoot = Join-Path $script:ArtifactsRoot 'publish'

$script:BuildBinlogPath = Join-Path $script:BinlogRoot 'build.binlog'
$script:FormatReportPath = Join-Path $script:AnalyzerRoot 'dotnet-format-report.json'
$script:EnvironmentLogPath = Join-Path $script:LogsRoot 'environment.log'

function New-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Get-DisplayPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $repoRoot = [System.IO.Path]::GetFullPath($script:RepoRoot)

    if ($fullPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($repoRoot.Length).TrimStart('\', '/')
    }

    return $fullPath
}

function Format-CommandLine {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $escapedArguments = foreach ($arg in $Arguments) {
        if ($arg -match '[\s"]') {
            '"{0}"' -f ($arg -replace '"', '\"')
        } else {
            $arg
        }
    }

    return '{0} {1}' -f $Executable, ($escapedArguments -join ' ')
}

function Test-TransientRestoreFailure {
    param([Parameter(Mandatory = $true)][string]$LogPath)

    if (-not (Test-Path -LiteralPath $LogPath)) {
        return $false
    }

    $logContent = Get-Content -Path $LogPath -Raw
    $patterns = @(
        'NU1301',
        'Unable to load the service index',
        'The SSL connection could not be established',
        'timed out',
        'Temporary failure in name resolution',
        'Name or service not known',
        'A connection attempt failed',
        '502 \(Bad Gateway\)',
        '503 \(Service Unavailable\)',
        '429 \(Too Many Requests\)'
    )

    foreach ($pattern in $patterns) {
        if ($logContent -match $pattern) {
            return $true
        }
    }

    return $false
}

function Get-CommonFixHint {
    param([string]$LogPath)

    if ([string]::IsNullOrWhiteSpace($LogPath) -or -not (Test-Path -LiteralPath $LogPath)) {
        return 'Inspect logs in .ci-artifacts/logs and attached diagnostics artifacts.'
    }

    $logContent = Get-Content -Path $LogPath -Raw

    if ($logContent -match 'NETSDK1045|does not support targeting|A compatible installed .NET SDK') {
        return 'SDK mismatch: install the SDK from global.json locally and ensure actions/setup-dotnet uses global.json in CI.'
    }

    if ($logContent -match 'NETSDK1147|workload(s)? must be installed') {
        return 'Missing workload: run dotnet workload restore (or install the required workload) before retrying.'
    }

    if ($logContent -match '401 \(Unauthorized\)|403 \(Forbidden\)|authentication failed|invalid authentication') {
        return 'NuGet authentication issue: verify feed credentials/secrets and confirm the feed source is reachable.'
    }

    if ($logContent -match 'NU1301|Unable to load the service index|timed out|The SSL connection could not be established') {
        return 'Transient feed/network issue: retry CI once; if persistent, verify nuget.org or private feed availability.'
    }

    return 'Review the first error in the failing log and inspect binlog/TRX diagnostics artifacts for details.'
}

function Write-EnvironmentHeader {
    $runnerOs = if ($env:RUNNER_OS) { $env:RUNNER_OS } else { [System.Runtime.InteropServices.RuntimeInformation]::OSDescription }
    $runnerArch = if ($env:RUNNER_ARCH) { $env:RUNNER_ARCH } else { [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() }
    $pwshVersion = '{0} ({1})' -f $PSVersionTable.PSVersion, $PSVersionTable.PSEdition

    $gitHead = 'unknown'
    $gitBranch = 'unknown'

    try {
        $gitHead = (git rev-parse HEAD 2>$null).Trim()
        if ($LASTEXITCODE -ne 0) {
            $gitHead = 'unknown'
        }
    } catch {
        $gitHead = 'unknown'
    }

    try {
        $gitBranch = (git rev-parse --abbrev-ref HEAD 2>$null).Trim()
        if ($LASTEXITCODE -ne 0) {
            $gitBranch = 'unknown'
        }
    } catch {
        $gitBranch = 'unknown'
    }

    Write-Host ''
    Write-Host '=== CI VERIFY ENVIRONMENT ===' -ForegroundColor Cyan
    Write-Host ('pwsh version : {0}' -f $pwshVersion)
    Write-Host ('runner       : {0} / {1}' -f $runnerOs, $runnerArch)
    Write-Host ('commit       : {0}' -f $gitHead)
    Write-Host ('branch       : {0}' -f $gitBranch)
    Write-Host 'dotnet --info:'

    @(
        '=== CI VERIFY ENVIRONMENT ===',
        ('pwsh version : {0}' -f $pwshVersion),
        ('runner       : {0} / {1}' -f $runnerOs, $runnerArch),
        ('commit       : {0}' -f $gitHead),
        ('branch       : {0}' -f $gitBranch),
        'dotnet --info:'
    ) | Out-File -FilePath $script:EnvironmentLogPath -Encoding utf8

    & dotnet --info 2>&1 | Tee-Object -FilePath $script:EnvironmentLogPath -Append
    if ($LASTEXITCODE -ne 0) {
        throw ('dotnet --info failed with exit code {0}.' -f $LASTEXITCODE)
    }
}

function Get-FormatIncludePaths {
    $paths = @()

    try {
        $rawChanged = @()

        if ($env:GITHUB_EVENT_NAME -eq 'pull_request' -and -not [string]::IsNullOrWhiteSpace($env:GITHUB_BASE_REF)) {
            $baseRef = 'origin/{0}' -f $env:GITHUB_BASE_REF

            & git rev-parse --verify $baseRef 2>$null | Out-Null
            if ($LASTEXITCODE -ne 0) {
                & git fetch --no-tags --depth=50 origin $env:GITHUB_BASE_REF 2>$null | Out-Null
            }

            $rawChanged = @(& git diff --name-only "$baseRef...HEAD" 2>$null)
        } else {
            $rawChanged = @(& git diff --name-only HEAD~1..HEAD 2>$null)
            if ($LASTEXITCODE -ne 0) {
                $rawChanged = @()
            }
        }

        foreach ($changedPath in $rawChanged) {
            if ([string]::IsNullOrWhiteSpace($changedPath)) {
                continue
            }

            $normalized = $changedPath.Replace('\\', '/')
            if ($normalized -notlike 'src/*') {
                continue
            }

            $extension = [System.IO.Path]::GetExtension($normalized)
            if ($extension -notin @('.cs', '.xaml', '.csproj', '.props', '.targets')) {
                continue
            }

            $absolutePath = Join-Path $script:RepoRoot $changedPath
            if (Test-Path -LiteralPath $absolutePath -PathType Leaf) {
                $paths += $normalized
            }
        }
    } catch {
        return @()
    }

    return @($paths | Sort-Object -Unique)
}

function Invoke-NativeStep {
    param(
        [Parameter(Mandatory = $true)][string]$StepName,
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$BaseLogPath,
        [ValidateRange(1, 5)][int]$MaxAttempts = 1,
        [switch]$RetryOnTransientNetwork
    )

    $commandLine = Format-CommandLine -Executable $Executable -Arguments $Arguments
    $logDirectory = Split-Path -Path $BaseLogPath -Parent
    $logName = [System.IO.Path]::GetFileNameWithoutExtension($BaseLogPath)
    $logExtension = [System.IO.Path]::GetExtension($BaseLogPath)

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $attemptLogPath = if ($MaxAttempts -gt 1) {
            Join-Path $logDirectory ('{0}.attempt{1}{2}' -f $logName, $attempt, $logExtension)
        } else {
            $BaseLogPath
        }

        New-Item -ItemType File -Path $attemptLogPath -Force | Out-Null
        ('[{0}] {1}' -f (Get-Date -Format 'u'), $commandLine) | Out-File -FilePath $attemptLogPath -Encoding utf8

        Write-Host ''
        Write-Host ('==> {0} (attempt {1}/{2})' -f $StepName, $attempt, $MaxAttempts) -ForegroundColor Cyan
        Write-Host ('    {0}' -f $commandLine) -ForegroundColor DarkGray

        $exitCode = 0

        try {
            & $Executable @Arguments 2>&1 | Tee-Object -FilePath $attemptLogPath -Append
            $exitCode = $LASTEXITCODE
        } catch {
            $_ | Out-String | Out-File -FilePath $attemptLogPath -Append -Encoding utf8
            $exitCode = 1
        }

        if ($exitCode -eq 0) {
            if ($attempt -gt 1) {
                Write-Host ('{0} succeeded on retry attempt {1}.' -f $StepName, $attempt) -ForegroundColor Yellow
            }

            return
        }

        $canRetry = $RetryOnTransientNetwork.IsPresent -and $attempt -lt $MaxAttempts -and (Test-TransientRestoreFailure -LogPath $attemptLogPath)

        if ($canRetry) {
            Write-Warning ('{0} failed due to transient network/feed issue. Retrying in {1} second(s)...' -f $StepName, $RestoreRetryDelaySeconds)
            Start-Sleep -Seconds $RestoreRetryDelaySeconds
            continue
        }

        $script:FailedStep = $StepName
        $script:TopFailingCommand = $commandLine
        $script:FailedLogPath = $attemptLogPath
        $script:FailedExitCode = $exitCode

        throw ('{0} failed with exit code {1}.' -f $StepName, $exitCode)
    }
}

function Write-CiSummary {
    param([Parameter(Mandatory = $true)][bool]$Succeeded)

    $duration = (Get-Date) - $script:StartTime
    $status = if ($Succeeded) { 'PASS' } else { 'FAIL' }
    $failedStep = if ([string]::IsNullOrWhiteSpace($script:FailedStep)) { 'none' } else { $script:FailedStep }
    $failedCommand = if ([string]::IsNullOrWhiteSpace($script:TopFailingCommand)) { 'none' } else { $script:TopFailingCommand }
    $failedLog = if ([string]::IsNullOrWhiteSpace($script:FailedLogPath)) { 'none' } else { Get-DisplayPath -Path $script:FailedLogPath }
    $hint = if ($Succeeded) { 'No action needed.' } else { Get-CommonFixHint -LogPath $script:FailedLogPath }

    $summaryLines = @(
        '## Verify Summary',
        '',
        ('- Status: **{0}**' -f $status),
        ('- Job: `{0}`' -f $(if ($env:GITHUB_JOB) { $env:GITHUB_JOB } else { 'local' })),
        ('- Runner: `{0}/{1}`' -f $(if ($env:RUNNER_OS) { $env:RUNNER_OS } else { 'local' }), $(if ($env:RUNNER_ARCH) { $env:RUNNER_ARCH } else { 'local' })),
        ('- Duration: `{0}`' -f ([System.Math]::Round($duration.TotalSeconds, 2))),
        ('- Top failing command: `{0}`' -f $failedCommand),
        ('- Failed step: `{0}`' -f $failedStep),
        ('- Exit code: `{0}`' -f $script:FailedExitCode),
        ('- Failed log: `{0}`' -f $failedLog),
        ('- Logs/artifacts root: `{0}`' -f (Get-DisplayPath -Path $script:ArtifactsRoot)),
        ('- Build binlog: `{0}`' -f (Get-DisplayPath -Path $script:BuildBinlogPath)),
        ('- Test results: `{0}`' -f (Get-DisplayPath -Path $script:TestResultsRoot)),
        ('- dotnet format report: `{0}`' -f (Get-DisplayPath -Path $script:FormatReportPath)),
        ('- Hint: {0}' -f $hint)
    )

    Write-Host ''
    Write-Host '===== CI SUMMARY =====' -ForegroundColor Cyan
    foreach ($line in $summaryLines) {
        Write-Host $line
    }
    Write-Host '======================' -ForegroundColor Cyan

    if ($env:GITHUB_STEP_SUMMARY) {
        $summaryLines | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Encoding utf8 -Append
        '' | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Encoding utf8 -Append
    }
}

Push-Location $script:RepoRoot

$success = $false

try {
    if (Test-Path -LiteralPath $script:ArtifactsRoot) {
        Remove-Item -Path $script:ArtifactsRoot -Recurse -Force
    }

    New-Directory -Path $script:ArtifactsRoot
    New-Directory -Path $script:LogsRoot
    New-Directory -Path $script:BinlogRoot
    New-Directory -Path $script:AnalyzerRoot
    New-Directory -Path $script:TestResultsRoot
    New-Directory -Path $script:PublishRoot

    Write-EnvironmentHeader

    $restoreArguments = @('restore', 'src/WsusManager.sln', '--nologo', '--verbosity', 'minimal')
    $lockFileCount = (Get-ChildItem -Path $script:RepoRoot -Filter 'packages.lock.json' -Recurse -File -ErrorAction SilentlyContinue | Measure-Object).Count

    if ($lockFileCount -gt 0) {
        $restoreArguments += '--locked-mode'
        Write-Host ('Using locked-mode restore ({0} lock file(s) detected).' -f $lockFileCount)
    } else {
        Write-Host 'No packages.lock.json found; running standard restore.'
    }

    Invoke-NativeStep `
        -StepName 'Restore' `
        -Executable 'dotnet' `
        -Arguments $restoreArguments `
        -BaseLogPath (Join-Path $script:LogsRoot 'restore.log') `
        -MaxAttempts $RestoreRetryCount `
        -RetryOnTransientNetwork

    Invoke-NativeStep `
        -StepName 'Build' `
        -Executable 'dotnet' `
        -Arguments @(
            'build',
            'src/WsusManager.sln',
            '--configuration', $Configuration,
            '--no-restore',
            '--no-incremental',
            '--nologo',
            '-warnaserror',
            ('/bl:{0}' -f $script:BuildBinlogPath)
        ) `
        -BaseLogPath (Join-Path $script:LogsRoot 'build.log')

    if ($SkipFormat) {
        Write-Host 'Skipping dotnet format (SkipFormat requested).' -ForegroundColor Yellow
        '{"status":"skipped","reason":"SkipFormat requested"}' | Out-File -FilePath $script:FormatReportPath -Encoding utf8
    } elseif (Test-Path -LiteralPath (Join-Path $script:RepoRoot 'src/.editorconfig')) {
        $formatInclude = @(Get-FormatIncludePaths)

        if ($formatInclude.Count -eq 0) {
            Write-Host 'No changed .NET source/config files detected; skipping dotnet format.' -ForegroundColor Yellow
            '{"status":"skipped","reason":"No changed files for format gate"}' | Out-File -FilePath $script:FormatReportPath -Encoding utf8
        } else {
            Write-Host ('Running dotnet format against {0} changed file(s).' -f $formatInclude.Count)

            $formatArguments = @(
                'format',
                'src/WsusManager.sln',
                '--verify-no-changes',
                '--no-restore',
                '--verbosity', 'minimal',
                '--report', $script:FormatReportPath,
                '--include'
            ) + $formatInclude

            Invoke-NativeStep `
                -StepName 'Format/Lint' `
                -Executable 'dotnet' `
                -Arguments $formatArguments `
                -BaseLogPath (Join-Path $script:LogsRoot 'format.log')
        }
    } else {
        Write-Host 'No src/.editorconfig found; skipping dotnet format.' -ForegroundColor Yellow
        '{"status":"skipped","reason":"src/.editorconfig not found"}' | Out-File -FilePath $script:FormatReportPath -Encoding utf8
    }

    $testArguments = @(
        'test',
        'src/WsusManager.Tests/WsusManager.Tests.csproj',
        '--configuration', $Configuration,
        '--no-build',
        '--verbosity', 'minimal',
        '--logger', 'trx;LogFileName=test-results.trx',
        '--results-directory', $script:TestResultsRoot,
        '--blame-hang-timeout', '15m',
        '--blame-hang-dump-type', 'mini',
        '--filter', 'FullyQualifiedName!~ExeValidation&FullyQualifiedName!~DistributionPackage'
    )

    if ($CollectCoverage) {
        $testArguments += '--settings'
        $testArguments += 'src/coverlet.runsettings'
        $testArguments += '--collect:XPlat Code Coverage'
    }

    Invoke-NativeStep `
        -StepName 'Tests' `
        -Executable 'dotnet' `
        -Arguments $testArguments `
        -BaseLogPath (Join-Path $script:LogsRoot 'test.log')

    if ($RunPackaging) {
        Invoke-NativeStep `
            -StepName 'Packaging' `
            -Executable 'dotnet' `
            -Arguments @(
                'publish',
                'src/WsusManager.App/WsusManager.App.csproj',
                '--configuration', $Configuration,
                '--output', $script:PublishRoot,
                '--self-contained', 'true',
                '--runtime', 'win-x64',
                '--no-restore',
                '-p:PublishSingleFile=true',
                '-p:IncludeAllContentForSelfExtract=true'
            ) `
            -BaseLogPath (Join-Path $script:LogsRoot 'publish.log')
    }

    $success = $true
}
catch {
    if ([string]::IsNullOrWhiteSpace($script:FailedStep)) {
        $script:FailedStep = 'Unhandled'
        $script:TopFailingCommand = $_.Exception.Message
        $script:FailedLogPath = Join-Path $script:LogsRoot 'unhandled.log'
        $_ | Out-String | Out-File -FilePath $script:FailedLogPath -Encoding utf8
        $script:FailedExitCode = 1
    }

    Write-Host ('::error::{0}' -f $_.Exception.Message)
}
finally {
    Write-CiSummary -Succeeded:$success
    Pop-Location
}

if (-not $success) {
    exit 1
}

exit 0
