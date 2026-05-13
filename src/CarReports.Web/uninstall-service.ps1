# Stops and removes the CarReports Windows Service.
# Run from an elevated PowerShell session.

$ErrorActionPreference = 'Stop'

$serviceName = 'CarReports'

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if (-not $existing) {
    Write-Host "Service '$serviceName' is not installed."
    return
}

Write-Host "Stopping service..."
Stop-Service -Name $serviceName -ErrorAction SilentlyContinue

Write-Host "Deleting service..."
& sc.exe delete $serviceName | Out-Null

Write-Host "Done."
