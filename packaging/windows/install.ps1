#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs Balsam API as a Windows Service.
.PARAMETER BindingMode
    "local" (default) binds to localhost only. "public" binds to 0.0.0.0 (accessible from other devices).
.PARAMETER Port
    Port number to listen on (default: 5000).
.PARAMETER InstallPath
    Installation directory (default: C:\Program Files\Balsam\API).
.EXAMPLE
    .\install.ps1
    .\install.ps1 -BindingMode public -Port 8080
#>
param(
    [ValidateSet("local", "public")]
    [string]$BindingMode,
    [int]$Port = 5000,
    [string]$InstallPath = "C:\Program Files\Balsam\API"
)

$ServiceName = "BalsamAPI"
$ExeName = "Balsam.API.exe"

Write-Host "=== Balsam API Installer ===" -ForegroundColor Cyan
Write-Host ""

# Prompt for binding mode if not provided
if (-not $BindingMode) {
    Write-Host "How should the API be accessible?" -ForegroundColor Yellow
    Write-Host "  [1] Local only  - only this computer (recommended for single-device setup)"
    Write-Host "  [2] Public      - accessible from other devices on the network"
    Write-Host ""
    $choice = Read-Host "Enter choice (1 or 2)"
    $BindingMode = if ($choice -eq "2") { "public" } else { "local" }
}

$Urls = if ($BindingMode -eq "public") { "http://0.0.0.0:$Port" } else { "http://localhost:$Port" }

Write-Host ""
Write-Host "Binding mode : $BindingMode ($Urls)" -ForegroundColor Green
Write-Host "Install path : $InstallPath" -ForegroundColor Green
Write-Host ""

# Stop existing service if running
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Create install directory
if (!(Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Copy files
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Copying files to $InstallPath..."
Copy-Item -Path "$ScriptDir\*" -Destination $InstallPath -Recurse -Force

# Write appsettings.Production.json
$config = @{
    Server = @{ Urls = $Urls }
    Database = @{
        Provider = "Sqlite"
        ConnectionString = "Data Source=balsam.db"
    }
} | ConvertTo-Json -Depth 3

Set-Content -Path "$InstallPath\appsettings.Production.json" -Value $config

# Set environment variable for the service
[System.Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")

# Register Windows Service
$exePath = Join-Path $InstallPath $ExeName
Write-Host "Registering Windows Service '$ServiceName'..."
sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto | Out-Null
sc.exe description $ServiceName "Balsam Healthcare API Server" | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

# Start the service
Write-Host "Starting service..."
Start-Service -Name $ServiceName

Write-Host ""
Write-Host "=== Installation complete ===" -ForegroundColor Green
Write-Host "Service '$ServiceName' is running on $Urls"
Write-Host ""
Write-Host "Test it:  curl $Urls/api/v1/health"
Write-Host "Logs:     Event Viewer > Windows Logs > Application"
Write-Host "Uninstall: Run uninstall.ps1 as Administrator"
