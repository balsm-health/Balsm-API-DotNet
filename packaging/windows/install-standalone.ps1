#Requires -RunAsAdministrator

# Balsm Standalone Installer for Windows
# Installs both the API and Supervisor as a Windows Service.

param(
    [string]$InstallPath = "C:\Program Files\Balsm",
    [int]$ApiPort = 5000,
    [int]$SupervisorPort = 5001,
    [ValidateSet("local", "public")]
    [string]$BindingMode = "local"
)

$ServiceName = "BalsmSupervisor"
$ServiceDisplayName = "Balsm Supervisor"
$ServiceDescription = "Balsm Healthcare Platform - Supervisor & Admin Panel"

$ApiDir = Join-Path $InstallPath "api"
$SupervisorDir = Join-Path $InstallPath "supervisor"
$SupervisorExe = Join-Path $SupervisorDir "Balsm.Supervisor.exe"

Write-Host "=== Balsm Standalone Installer ===" -ForegroundColor Cyan
Write-Host "Install path:    $InstallPath"
Write-Host "Binding mode:    $BindingMode"
Write-Host "API port:        $ApiPort"
Write-Host "Supervisor port: $SupervisorPort"
Write-Host ""

# Stop existing service if running
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Create directories
New-Item -ItemType Directory -Force -Path $ApiDir | Out-Null
New-Item -ItemType Directory -Force -Path $SupervisorDir | Out-Null

# Copy files (assumes script is run from the extracted bundle directory)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BundleApiDir = Join-Path $ScriptDir "api"
$BundleSupervisorDir = Join-Path $ScriptDir "supervisor"

if (Test-Path $BundleApiDir) {
    Write-Host "Copying API files..."
    Copy-Item -Path "$BundleApiDir\*" -Destination $ApiDir -Recurse -Force
}

if (Test-Path $BundleSupervisorDir) {
    Write-Host "Copying Supervisor files..."
    Copy-Item -Path "$BundleSupervisorDir\*" -Destination $SupervisorDir -Recurse -Force
}

# Generate API appsettings.Production.json
$ApiUrls = if ($BindingMode -eq "public") { "http://0.0.0.0:$ApiPort" } else { "http://localhost:$ApiPort" }
$ApiConfig = @{
    Server = @{ Urls = $ApiUrls }
    Database = @{
        Provider = "Sqlite"
        ConnectionString = "Data Source=balsm.db"
    }
} | ConvertTo-Json -Depth 3

Set-Content -Path (Join-Path $ApiDir "appsettings.Production.json") -Value $ApiConfig

Write-Host "Installing Windows Service..."

sc.exe create $ServiceName `
    binPath= "`"$SupervisorExe`"" `
    start= auto `
    DisplayName= "`"$ServiceDisplayName`"" | Out-Null

sc.exe description $ServiceName "`"$ServiceDescription`"" | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Host "Starting service..."
Start-Service -Name $ServiceName

Write-Host ""
Write-Host "=== Installation Complete ===" -ForegroundColor Green
Write-Host "Supervisor: http://localhost:$SupervisorPort"
Write-Host "API:        http://localhost:$ApiPort"
Write-Host ""
Write-Host "Open http://localhost:$SupervisorPort in your browser to access the Admin Panel."
