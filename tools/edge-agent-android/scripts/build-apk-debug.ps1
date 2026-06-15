param(
    [string]$ProjectRoot = "../app"
)

$ErrorActionPreference = "Stop"

if (-not $env:JAVA_HOME -or -not (Test-Path (Join-Path $env:JAVA_HOME "bin\java.exe"))) {
    $candidate = "C:/Users/Oh-z600-A/AppData/Local/Programs/Microsoft/jdk-17.0.10.7-hotspot"
    if (Test-Path (Join-Path $candidate "bin/java.exe")) {
        $env:JAVA_HOME = $candidate
        if (-not ($env:Path -split ";" | Where-Object { $_ -eq (Join-Path $candidate "bin") })) {
            $env:Path = "$(Join-Path $candidate "bin");$env:Path"
        }
    }
}

$projectPath = Resolve-Path (Join-Path $PSScriptRoot $ProjectRoot)
Push-Location $projectPath
try {
    if (-not (Test-Path "./gradlew.bat")) {
        throw "gradlew.bat was not found. Ensure Android Gradle project exists under app/."
    }

    if (-not $env:JAVA_HOME -or -not (Test-Path (Join-Path $env:JAVA_HOME "bin\java.exe"))) {
        throw "JAVA_HOME is not configured. Install JDK 17 or set JAVA_HOME before running this script."
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
