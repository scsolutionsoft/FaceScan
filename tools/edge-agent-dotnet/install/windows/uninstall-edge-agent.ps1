param(
    [string]$ServiceName = "FaceScanEdgeAgent"
)

$ErrorActionPreference = "Continue"
sc.exe stop $ServiceName | Out-Null
Start-Sleep -Seconds 1
sc.exe delete $ServiceName | Out-Null
Write-Host "Uninstalled service $ServiceName"
