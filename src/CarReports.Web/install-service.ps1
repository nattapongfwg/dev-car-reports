# Installs CarReports as a Windows Service.
# Run from an elevated PowerShell session in the folder that contains CarReports.Web.exe.

$ErrorActionPreference = 'Stop'

$serviceName = 'CarReports'
$exe = Join-Path $PSScriptRoot 'CarReports.Web.exe'

if (-not (Test-Path $exe)) {
    throw "CarReports.Web.exe not found next to this script ($PSScriptRoot)."
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service '$serviceName' already exists. Stopping it first..."
    Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
    & sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating service '$serviceName' pointing at $exe"
New-Service -Name $serviceName `
            -BinaryPathName "`"$exe`"" `
            -DisplayName 'Car Reports Web' `
            -StartupType Automatic `
            -Description 'Excel upload/transform web app (Car Reports).' | Out-Null

Write-Host "Starting service..."
Start-Service -Name $serviceName

Get-Service -Name $serviceName
Write-Host ""
Write-Host "Done. The app is now running on the URL configured in appsettings.Production.json"
Write-Host "(default: http://localhost:5000)."
