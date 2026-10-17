#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls Balsm API Windows Service.
.PARAMETER RemoveData
    If specified, also removes the database and configuration files.
.PARAMETER InstallPath
    Installation directory (default: C:\Program Files\Balsm\API).
#>
param(
    [switch]$RemoveData,
    [string]$InstallPath = "C:\Program Files\Balsm\API"
)

$ServiceName = "BalsmAPI"

Write-Host "=== Balsm API Uninstaller ===" -ForegroundColor Cyan
Write-Host ""

# Stop and remove service
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping service '$ServiceName'..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Write-Host "Removing service..."
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
    Write-Host "  Service removed." -ForegroundColor Green
} else {
    Write-Host "  Service '$ServiceName' not found, skipping." -ForegroundColor Yellow
}

# Remove files
if (Test-Path $InstallPath) {
    if (-not $RemoveData) {
        Write-Host ""
        $confirm = Read-Host "Remove all files at '$InstallPath'? This includes the database. (y/N)"
        $RemoveData = $confirm -eq "y"
    }

    if ($RemoveData) {
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "  Files removed." -ForegroundColor Green
    } else {
        # Remove only the executable and DLLs, keep data
        Get-ChildItem -Path $InstallPath -Exclude "*.db", "appsettings.Production.json", "logs" |
            Remove-Item -Recurse -Force
        Write-Host "  Executable removed. Config and database preserved." -ForegroundColor Green
    }
}

# Clean up environment variable
[System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", $null, "Machine")

Write-Host ""
Write-Host "=== Uninstall complete ===" -ForegroundColor Green
