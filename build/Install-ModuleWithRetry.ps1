param(
    [Parameter(Mandatory)]
    [string]$ModuleName,
    [string]$MinimumVersion,
    [int]$Attempts = 3,
    [int]$DelaySeconds = 5,
    [switch]$AllowClobber
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Set-PSRepository -Name PSGallery -InstallationPolicy Trusted -ErrorAction Stop

$installParams = @{ 
    Name = $ModuleName
    Scope = 'CurrentUser'
    Force = $true
}

if ($MinimumVersion) {
    $installParams.MinimumVersion = $MinimumVersion
}

if ($AllowClobber) {
    $installParams.AllowClobber = $true
}

$lastError = $null
for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
    try {
        Install-Module @installParams
        return
    } catch {
        $lastError = $_
        if ($attempt -lt $Attempts) {
            Write-Host "Attempt $attempt installing $ModuleName failed, retrying in $DelaySeconds seconds..." -ForegroundColor Yellow
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}

throw $lastError
