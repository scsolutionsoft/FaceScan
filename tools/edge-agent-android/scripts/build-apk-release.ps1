param(
    [string]$ProjectRoot = "../app",
    [string]$VersionName = "0.1.0"
)

$ErrorActionPreference = "Stop"

$projectPath = Resolve-Path (Join-Path $PSScriptRoot $ProjectRoot)
Push-Location $projectPath
try {
    if (-not (Test-Path "./gradlew.bat")) {
        throw "gradlew.bat was not found. Ensure Android Gradle project exists under app/."
    }

    .\gradlew.bat assembleRelease
    if ($LASTEXITCODE -ne 0) {
        throw "assembleRelease failed"
    }

    $builtApk = Get-ChildItem -Path ".\app\build\outputs\apk\release" -Filter "*.apk" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $builtApk) {
        throw "APK was not found after assembleRelease."
    }

    $target = Resolve-Path "../apk/release"
    $name = "facescan-edge-agent-v$VersionName-release.apk"
    Copy-Item $builtApk.FullName (Join-Path $target $name) -Force
    Write-Host "Release APK ready: $(Join-Path $target $name)" -ForegroundColor Green
}
finally {
    Pop-Location
}
