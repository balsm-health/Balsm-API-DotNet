#Requires -RunAsAdministrator

# Balsam Standalone Uninstaller for Windows

param(
    [string]$InstallPath = "C:\Program Files\Balsam",
    [switch]$KeepData
)

$ServiceName = "BalsamSupervisor"

Write-Host "=== Balsam Standalone Uninstaller ===" -ForegroundColor Cyan

# Stop and remove service
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "Removing service..."
    sc.exe delete $ServiceName | Out-Null
}

if ($KeepData) {
    Write-Host "Keeping data files (database, configuration)."
    Write-Host "Removing executables only..."
    # Remove executables but keep .db and .json files
    Get-ChildItem -Path $InstallPath -Recurse -File |
        Where-Object { $_.Extension -notin @('.db', '.json', '.txt') } |
        Remove-Item -Force -ErrorAction SilentlyContinue
} else {
    if (Test-Path $InstallPath) {
        Write-Host "Removing all files at $InstallPath..."
        Remove-Item -Path $InstallPath -Recurse -Force
    }
}

Write-Host ""
Write-Host "=== Uninstallation Complete ===" -ForegroundColor Green
