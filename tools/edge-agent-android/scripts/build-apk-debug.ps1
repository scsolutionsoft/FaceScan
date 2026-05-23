param(
    [string]$ProjectRoot = "../app"
)

$ErrorActionPreference = "Stop"

$projectPath = Resolve-Path (Join-Path $PSScriptRoot $ProjectRoot)
Push-Location $projectPath
try {
    if (-not (Test-Path "./gradlew.bat")) {
        throw "gradlew.bat was not found. Ensure Android Gradle project exists under app/."
    }

    .\gradlew.bat assembleDebug
    if ($LASTEXITCODE -ne 0) {
        throw "assembleDebug failed"
    }

    $builtApk = Get-ChildItem -Path ".\app\build\outputs\apk\debug" -Filter "*.apk" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $builtApk) {
        throw "APK was not found after assembleDebug."
    }

    $target = Resolve-Path "../apk/debug"
    $outputPath = Join-Path $target "facescan-edge-agent-debug.apk"
    Copy-Item $builtApk.FullName $outputPath -Force
    Write-Host "Debug APK ready: $outputPath" -ForegroundColor Green
}
finally {
    Pop-Location
}
