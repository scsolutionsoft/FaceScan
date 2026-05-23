param(
    [string]$ProjectRoot = "../app",
    [string]$VersionName = "0.1.0"
)

$ErrorActionPreference = "Stop"

$projectPath = Resolve-Path (Join-Path $PSScriptRoot $ProjectRoot)
Push-Location $projectPath
try {
    if (-not (Test-Path "./gradlew.bat")) {
        throw "gradlew.bat was not found. Run scripts/init-gradle-wrapper.ps1 first."
    }

    if (-not (Test-Path "./keystore.properties")) {
        throw "keystore.properties was not found. Copy from keystore.properties.sample and fill signing values."
    }

    .\gradlew.bat clean assembleRelease
    if ($LASTEXITCODE -ne 0) {
        throw "assembleRelease failed"
    }

    $builtApk = Get-ChildItem -Path ".\app\build\outputs\apk\release" -Filter "*.apk" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $builtApk) {
        throw "Release APK was not found after assembleRelease."
    }

    $target = Resolve-Path "../apk/release"
    $name = "facescan-edge-agent-v$VersionName-signed-release.apk"
    Copy-Item $builtApk.FullName (Join-Path $target $name) -Force
    Write-Host "Signed Release APK ready: $(Join-Path $target $name)" -ForegroundColor Green
}
finally {
    Pop-Location
}
