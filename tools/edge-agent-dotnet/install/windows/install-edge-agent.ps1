param(
    [string]$InstallDir = "C:\ProgramData\FaceScan\EdgeAgent",
    [string]$ServiceName = "FaceScanEdgeAgent",
    [string]$DisplayName = "FaceScan Edge Agent",
    [string]$BinaryPath = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $InstallDir)) {
    New-Item -Path $InstallDir -ItemType Directory -Force | Out-Null
}

if ([string]::IsNullOrWhiteSpace($BinaryPath)) {
    $BinaryPath = Join-Path $PSScriptRoot "..\..\publish\dist\win-x64\FaceScan.EdgeAgent.exe"
}

if (-not (Test-Path $BinaryPath)) {
    throw "Binary not found: $BinaryPath"
}

Copy-Item $BinaryPath (Join-Path $InstallDir "FaceScan.EdgeAgent.exe") -Force

$envFileSource = Join-Path $PSScriptRoot "..\..\FaceScan.EdgeAgent\edge-agent.sample.env"
$envFileTarget = Join-Path $InstallDir "edge-agent.env"
if (-not (Test-Path $envFileTarget)) {
    Copy-Item $envFileSource $envFileTarget -Force
}

$escaped = '"' + (Join-Path $InstallDir "FaceScan.EdgeAgent.exe") + '"'
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    sc.exe stop $ServiceName | Out-Null
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

sc.exe create $ServiceName binPath= $escaped start= auto DisplayName= '"'"$DisplayName"'"' | Out-Null
sc.exe description $ServiceName '"'"Walk-by IP camera edge agent for FaceScan"'"' | Out-Null
sc.exe start $ServiceName | Out-Null

Write-Host "Installed service $ServiceName" -ForegroundColor Green
Write-Host "Edit env file: $envFileTarget" -ForegroundColor Yellow
